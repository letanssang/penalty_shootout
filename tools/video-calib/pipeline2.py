#!/usr/bin/env python3
"""Video -> CSV quy dao 3D (time,x,y,z) cho ParameterFitter cua Unity."""
import os, sys, cv2, numpy as np
import auto_calib as ac, frame_calib as fc, railpose as rp, ball_track as bt
import pipeline as pl, flight as fl

W_, H_ = 1920, 1080
R_BALL = 0.10984


def static_end(load, rends, sb):
    """Bam bong dung yen; tra ve (chi so render luc cham chan, vi tri)."""
    pos, k, miss = sb, 0, 0
    for j, i in enumerate(rends[1:], 1):
        o = bt.refine_radial(bt.ballness(load(i)), pos[0], pos[1], pos[2])
        if o is None or o[3] > 1.2:
            miss += 1
            if miss > 6: break
            continue
        if np.hypot(o[0]-pos[0], o[1]-pos[1]) > 12:
            break
        pos, k, miss = o, j, 0
    return k, pos


def run(vid, verbose=True):
    d = 'fr%d' % vid
    nfr = len([x for x in os.listdir(d) if x.endswith('.jpg')])
    load = lambda i: cv2.imread('%s/%04d.jpg' % (d, i))
    f, y0, ncal = pl.calib(d, nfr)
    K = np.array([[f,0,W_/2.],[0,f,H_/2.],[0,0,1.]]); Ki = np.linalg.inv(K)
    si, sb = pl.seed_ball(d, f, y0, 20, min(nfr, 160))
    rends = bt.distinct_frames(load, si, nfr - 1)
    k, pos = static_end(load, rends, sb)
    fi, fb = fl.find_seed(load, rends[k+1:k+11], sb[2], (pos[0], pos[1]))
    if fi is None:
        print('video %d: khong mo duoc pha bay' % vid); return None
    kf = rends.index(fi)
    fwd = rends[kf:]
    bwd = rends[k+1:kf+1][::-1]
    res = {}
    res.update(bt.track(bwd, load, (fi, *fb[:3])))
    res.update(bt.track(fwd, load, (fi, *fb[:3])))
    fr = sorted(res)
    if verbose:
        print('video %d | f=%.1f cao=%.4f | tinh->khung %d | mo bay %d | bam %d khung (%d..%d)'
              % (vid, f, y0, rends[k], fi, len(fr), fr[0], fr[-1]))

    # tu the camera tren tung khung bay
    pose, keep = {}, []
    for i in fr:
        try: m = fc.measure(load(i))
        except Exception: continue
        rms, z, pan, tilt = rp.solve_rail(m, f, y0=y0)
        if rms > 1.5: continue
        pose[i] = (z, pan, tilt); keep.append(i)
    if len(keep) < 8:
        print('  qua it khung co tu the tot (%d)' % len(keep)); return None

    resid, alpha, phi, ticks = pl.assign_ticks(np.array([pose[i][0] for i in keep]))
    if verbose:
        print('  gan tick: nhip render %.2f fps, pha %.2f, do khong tron cam_z %.4f m'
              % (50.0/alpha, phi, resid))

    rows = []
    for i, n in zip(keep, ticks):
        z, pan, tilt = pose[i]
        R, t = rp.extrinsics(z, pan, tilt, y0=y0)
        C = np.array([0.0, y0, z])
        cx, cy, r = res[i][0], res[i][1], res[i][2]
        u = Ki @ np.array([cx, cy, 1.0]); u = (R.T @ u.reshape(3,1)).ravel(); u /= np.linalg.norm(u)
        P = C + (f * R_BALL / r) * u
        rows.append((int(n), P[0], P[1], P[2], i, res[i][3]))
    return dict(vid=vid, f=f, y0=y0, rows=rows, alpha=alpha)


def polish(rows, verbose=True):
    """Bo diem lac (bong de len luoi/vach voi) bang phan du bac hai."""
    a = np.array([[n*0.02, x, y, z] for n, x, y, z, i, s in rows])
    idx = np.arange(len(a))
    for _ in range(3):
        t = a[idx, 0]
        A = np.stack([t**k for k in range(3)], axis=1)
        bad = []
        for col in (1, 2, 3):
            c, *_ = np.linalg.lstsq(A, a[idx, col], rcond=None)
            r = a[idx, col] - A @ c
            s = max(1.4826*np.median(np.abs(r)), 0.02)
            bad += list(idx[np.abs(r) > 4*s])
        if not bad: break
        idx = np.array([j for j in idx if j not in set(bad)])
    if verbose and len(idx) < len(a):
        print('  bo %d diem lac' % (len(a)-len(idx)))
    return a[idx]


if __name__ == '__main__':
    os.makedirs('out', exist_ok=True)
    for v in [int(x) for x in sys.argv[1:]] or [1,2,3,4,5]:
        r = run(v)
        if r is None: continue
        a = polish(r['rows'])
        a[:, 0] -= a[0, 0]
        t = a[:,0]
        A = np.stack([t**k for k in range(3)], axis=1)
        cy_, *_ = np.linalg.lstsq(A, a[:,2], rcond=None)
        rms = np.sqrt(((a[:,2] - A@cy_)**2).mean())
        print('  %d diem | g=%.3f m/s^2 | v0 ~ %.1f m/s | rms doc %.4f m'
              % (len(a), -2*cy_[2],
                 np.hypot(np.polyfit(t, a[:,1], 2)[1], np.polyfit(t, a[:,3], 2)[1]), rms))
        with open('out/shot%d.csv' % v, 'w') as fh:
            fh.write('time,x,y,z\n')
            for row in a:
                fh.write('%.4f,%.5f,%.5f,%.5f\n' % tuple(row))
        print('  -> out/shot%d.csv' % v)
