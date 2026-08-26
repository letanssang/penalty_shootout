#!/usr/bin/env python3
"""Duong ong day du: video -> quy dao 3D (time,x,y,z) cho ParameterFitter."""
import os, sys, cv2, numpy as np
import auto_calib as ac, frame_calib as fc, railpose as rp, ball_track as bt

W_, H_ = 1920, 1080
R_BALL = 0.10984            # ban kinh bong do duoc tu rang buoc cham dat


def calib(dirn, nfr):
    """f va cao camera Y0, lay trung vi tren cac khung giai duoc 6 diem."""
    fs, ys = [], []
    for i in range(6, min(nfr, 200), 2):
        img = cv2.imread('%s/%04d.jpg' % (dirn, i))
        if img is None: continue
        try: m = fc.measure(img)
        except Exception: continue
        b = fc.solve6(m)
        if b is None: continue
        rms, f, rv, tv, C = b
        if rms < 0.8 and 2000 < f < 3600:
            fs.append(f); ys.append(C[1])
        if len(fs) >= 15: break
    if not fs:
        return None
    return float(np.median(fs)), float(np.median(ys)), len(fs)


def seed_ball(dirn, f, y0, lo, hi):
    """Tim bong dung yen: chieu cham phat den xuong anh roi do quanh do."""
    K = np.array([[f,0,W_/2.],[0,f,H_/2.],[0,0,1.]])
    for i in range(lo, hi):
        img = cv2.imread('%s/%04d.jpg' % (dirn, i))
        if img is None: continue
        try: m = fc.measure(img)
        except Exception: continue
        rms, z, pan, tilt = rp.solve_rail(m, f, y0=y0)
        if rms > 1.0: continue
        R, t = rp.extrinsics(z, pan, tilt, y0=y0)
        q = rp.project(np.array([[0., R_BALL, 11.19]]), R, t, K)[0]
        d = np.linalg.norm(np.array([0., R_BALL, 11.19]) - np.array([0., y0, z]))
        rpx = f * R_BALL / d
        bn = bt.ballness(img)
        best = None
        for cxy in [(q[0], q[1], rpx)] + [(c[0], c[1], max(c[2], 4.)) for c in
                                          bt.blob_candidates(bn, q[0], q[1], 70., rpx)[:6]]:
            o = bt.refine_radial(bn, *cxy)
            if o is None or o[3] > 1.0: continue
            if abs(o[2] - rpx) > 0.35 * rpx: continue
            if np.hypot(o[0]-q[0], o[1]-q[1]) > 70: continue
            if best is None or o[3] < best[3]: best = o
        if best is not None:
            return i, best
    return None, None


def assign_ticks(zs, deg=3):
    """Gan chi so tick 50Hz cho tung khung render: quet (nhip, pha)."""
    r = np.arange(len(zs), dtype=float)
    best = None
    for alpha in np.arange(1.02, 1.42, 0.002):
        for phi in np.arange(0., 1., 0.02):
            n = np.floor(alpha * r + phi).astype(int)
            if np.any(np.diff(n) < 1):
                continue
            A = np.stack([n.astype(float)**k for k in range(deg+1)], axis=1)
            c, *_ = np.linalg.lstsq(A, zs, rcond=None)
            res = float(np.sqrt(((zs - A @ c)**2).mean()))
            if best is None or res < best[0]:
                best = (res, alpha, phi, n)
    return best


def run(vid, kick_hint=None, verbose=True):
    dirn = 'fr%d' % vid
    nfr = len([x for x in os.listdir(dirn) if x.endswith('.jpg')])
    load = lambda i: cv2.imread('%s/%04d.jpg' % (dirn, i))
    c = calib(dirn, nfr)
    if c is None:
        print('video %d: khong hieu chinh duoc' % vid); return None
    f, y0, ncal = c
    K = np.array([[f,0,W_/2.],[0,f,H_/2.],[0,0,1.]]); Ki = np.linalg.inv(K)
    if verbose:
        print('video %d: %d anh | f=%.1f  cao camera=%.4f m  (%d khung hieu chinh)'
              % (vid, nfr, f, y0, ncal))

    si, sb = seed_ball(dirn, f, y0, 20, min(nfr, 140))
    if si is None:
        print('  khong tim thay bong dung yen'); return None
    if verbose:
        print('  bong tinh o khung %d: (%.1f, %.1f) r=%.2f rms=%.2f' % (si, sb[0], sb[1], sb[2], sb[3]))

    fw = bt.distinct_frames(load, si, min(nfr, si + 60))
    res = bt.track(fw, load, (si, sb[0], sb[1], sb[2]), verbose=False)
    frames = sorted(res)
    if verbose:
        print('  bam duoc %d khung render: %d..%d' % (len(frames), frames[0], frames[-1]))

    # thoi diem sut: buoc dich chuyen anh vuot han muc dung yen
    step = np.array([np.hypot(res[frames[k+1]][0]-res[frames[k]][0],
                              res[frames[k+1]][1]-res[frames[k]][1])
                     for k in range(len(frames)-1)])
    kick = None
    for k in range(len(step)):
        if step[k] > 8.0 and (k+3 >= len(step) or np.all(step[k:k+3] > 6.0)):
            kick = k; break
    if kick is None:
        print('  khong xac dinh duoc thoi diem sut'); return None
    flight = frames[kick:]
    if verbose:
        print('  sut tai khung %d -> %d khung bay' % (frames[kick], len(flight)))
    return dict(vid=vid, dirn=dirn, f=f, y0=y0, K=K, Ki=Ki, res=res,
                frames=frames, flight=flight, static=frames[:kick+1])


if __name__ == '__main__':
    for v in [int(x) for x in sys.argv[1:]] or [1]:
        r = run(v)
        if r: np.save('stage1_%d.npy' % v, np.array([[i, *r['res'][i]] for i in r['frames']]))
