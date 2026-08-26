#!/usr/bin/env python3
"""Tu dong do hinh hoc san penalty trong khung hinh eFootball voi do chinh xac
duoi pixel, roi giai PnP tim tieu cu + tu the camera.

Cac moc do duoc:
  - 2 cot doc khung thanh (mep trong, duoi-pixel)
  - mep duoi xa ngang
  - chan cot (duong cau mon, z=0)
  - 3 vach voi ngang tren san: cau mon (0m), 5m50, 16m50 -> lay tam vach
  - qua bong dang dat tai cham phat den (11m)

Xuat ra file <video>.calib.json de extract_trajectory.py dung lai.
"""
import json
import sys

import cv2
import numpy as np
from scipy.optimize import minimize_scalar

GOAL_HALF_W = 3.66     # 7.32 m / 2, do giua 2 mep trong cot
GOAL_H = 2.44          # tu mat san toi mep duoi xa ngang
BALL_R = 0.11
SIX_YARD = 5.5
PEN_DIST = 11.0
PEN_AREA = 16.5


def white_mask(img):
    hsv = cv2.cvtColor(img, cv2.COLOR_BGR2HSV)
    _, S, V = cv2.split(hsv)
    return ((V > 170) & (S < 60)).astype(np.uint8)


def subpix_edge(profile, i0, i1, rising):
    """Vi tri duoi-pixel noi profile cat muc 50% giua nen toi va vung sang.

    profile[i0:i1] phai di tu toi -> sang (rising) hoac sang -> toi."""
    seg = profile[i0:i1].astype(np.float64)
    lo, hi = float(seg.min()), float(seg.max())
    if hi - lo < 20:
        return None
    half = 0.5 * (lo + hi)
    idx = range(len(seg) - 1) if rising else range(len(seg) - 1)
    for k in idx:
        a, b = seg[k], seg[k + 1]
        if (rising and a < half <= b) or (not rising and a >= half > b):
            t = (half - a) / (b - a)
            return i0 + k + t
    return None


def find_posts(img, wm, y0=250, y1=520):
    """Tra ve (mep trong trai, mep trong phai) duoi-pixel + dai y cua cot."""
    band = wm[y0:y1, :]
    colsum = band.sum(axis=0)
    strong = np.nonzero(colsum > band.shape[0] * 0.45)[0]
    if len(strong) < 2:
        raise RuntimeError("khong tim thay cot khung thanh")
    # gom thanh cac cum lien tuc
    groups, cur = [], [strong[0]]
    for x in strong[1:]:
        if x - cur[-1] <= 3:
            cur.append(x)
        else:
            groups.append(cur)
            cur = [x]
    groups.append(cur)
    groups = [g for g in groups if len(g) >= 5]
    groups.sort(key=lambda g: colsum[g].sum(), reverse=True)
    if len(groups) < 2:
        raise RuntimeError("chi tim thay %d cot" % len(groups))
    two = sorted(groups[:2], key=lambda g: g[0])
    left, right = two[0], two[1]

    # Camera chuc xuong ~6 deg nen khung thanh bi keystone: mep trong cot khong
    # thang dung. Do mep trong theo tung hang roi khop duong thang x = a*y + b.
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

    def edge_line(group, rising, ya, yb):
        ys, xs = [], []
        for y in range(ya, yb):
            row = gray[y - 1:y + 2, :].mean(axis=0)
            if rising:
                x_in, x_post = group[0] - 6, group[0] + 4
                inside = np.median(row[x_in:x_in + 3])
                post = np.median(row[x_post:x_post + 4])
            else:
                x_in, x_post = group[-1] + 4, group[-1] - 4
                inside = np.median(row[x_in:x_in + 3])
                post = np.median(row[x_post:x_post + 4])
            # Luoi phia trong doi khi sang gan bang cot (co luc con sang hon) ->
            # hang do khong con mep that, bo qua thay vi doan bua.
            if post - inside < 40:
                continue
            if rising:
                e = subpix_edge(row, group[0] - 6, group[0] + 4, rising=True)
            else:
                e = subpix_edge(row, group[-1] - 3, group[-1] + 6, rising=False)
            if e is not None:
                ys.append(y)
                xs.append(e)
        if len(ys) < 40:
            raise RuntimeError("qua it mep trong cot do duoc (%d hang)" % len(ys))
        ys = np.array(ys, float)
        xs = np.array(xs, float)
        # khoi tao bang Theil-Sen (trung vi cac he so goc) roi siet dan bang MAD
        idx = np.arange(len(ys))
        sl_ = [(xs[j] - xs[i]) / (ys[j] - ys[i])
               for i in idx[::3] for j in idx[::3] if ys[j] - ys[i] > 60]
        a = float(np.median(sl_))
        b = float(np.median(xs - a * ys))
        keep = np.ones(len(ys), bool)
        for _ in range(4):
            r = xs - (a * ys + b)
            mad = float(np.median(np.abs(r - np.median(r))))
            keep = np.abs(r - np.median(r)) < max(2.5 * 1.4826 * mad, 0.35)
            if keep.sum() < 25:
                break
            a, b = np.polyfit(ys[keep], xs[keep], 1)
        return a, b, float(np.std(xs[keep] - (a * ys[keep] + b)))

    # pham vi doc cua cot, suy tu chinh mat na trang (thich ung tung video)
    def vrun(group):
        w = len(group)
        rs = wm[:, group[0]:group[-1] + 1].sum(axis=1)
        ys = np.nonzero(rs > w * 0.5)[0]
        ys = ys[(ys > 150) & (ys < 800)]
        return int(ys.min()), int(ys.max())

    t_l, b_l = vrun(left)
    t_r, b_r = vrun(right)
    y_top, y_bot = max(t_l, t_r), min(b_l, b_r)
    ya, yb = y_top + 26, y_bot - 8
    return (edge_line(left, False, ya, yb), edge_line(right, True, ya, yb),
            left, right, y_top, y_bot)


def find_crossbar_and_base(img, left, right, y_top, y_bot):
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    xc_l = int(np.mean(left))
    xc_r = int(np.mean(right))

    # mep duoi xa ngang: lay o vung ngay ben trong cot, noi chi co xa ngang trang
    col = gray[:, xc_l + 12:xc_l + 28].mean(axis=1)
    y_cb = subpix_edge(col, y_top + 3, y_top + 26, rising=False)

    # chan cot: profile doc ngay tren truc cot
    ybases = []
    for xc in (xc_l, xc_r):
        c = gray[:, xc - 3:xc + 4].mean(axis=1)
        yb = subpix_edge(c, y_bot - 10, y_bot + 12, rising=False)
        if yb is not None:
            ybases.append(yb)
    if y_cb is None or not ybases:
        raise RuntimeError("khong do duoc xa ngang / chan cot")
    return y_cb, float(np.mean(ybases))


def ridge_profile(img, xa=700, xb=1220, k=7):
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY).astype(np.float32)
    col = gray[:, xa:xb].mean(axis=1)
    return col - 0.5 * (np.roll(col, k) + np.roll(col, -k))


def ridge_peaks(ridge, y_from, y_to, thresh=12.0):
    """Dinh cac vach voi ngang, tra ve tam duoi-pixel theo trong so."""
    out = []
    y = y_from
    while y < y_to - 1:
        if ridge[y] > thresh and ridge[y] >= ridge[y - 1] and ridge[y] >= ridge[y + 1]:
            w = ridge[y - 4:y + 5].clip(min=0)
            ys = np.arange(y - 4, y + 5)
            out.append(float((w * ys).sum() / w.sum()) if w.sum() > 0 else float(y))
            y += 8
        else:
            y += 1
    return out


def find_ball(img, wm, ysearch, xsearch):
    sub = wm[ysearch[0]:ysearch[1], xsearch[0]:xsearch[1]]
    n, lab, stats, cent = cv2.connectedComponentsWithStats(sub, 8)
    best = None
    for i in range(1, n):
        x, y, w, h, a = stats[i]
        if a < 120 or w > 70 or h > 70:
            continue
        if best is None or a > stats[best][4]:
            best = i
    if best is None:
        raise RuntimeError("khong tim thay bong")
    x, y, w, h, a = stats[best]
    cx = xsearch[0] + cent[best][0]
    cy = ysearch[0] + cent[best][1]
    # ban kinh tu dien tich (blob trang bi khuyet hoa tiet toi -> dung be rong)
    r = 0.5 * max(w, h)
    return float(cx), float(cy), float(r)


def solve_f(objp, imgp, W, H):
    def rms_of(f):
        K = np.array([[f, 0, W / 2.0], [0, f, H / 2.0], [0, 0, 1.0]])
        ok, rvec, tvec = cv2.solvePnP(objp, imgp, K, None,
                                      flags=cv2.SOLVEPNP_ITERATIVE)
        if not ok:
            return 1e9, None, None
        proj, _ = cv2.projectPoints(objp, rvec, tvec, K, None)
        d = proj.reshape(-1, 2) - imgp
        return float(np.sqrt((d ** 2).sum(axis=1).mean())), rvec, tvec

    fs = np.linspace(W * 0.5, W * 3.0, 161)
    rs = [rms_of(f)[0] for f in fs]
    f0 = fs[int(np.argmin(rs))]
    res = minimize_scalar(lambda lf: rms_of(float(np.exp(lf)))[0],
                          bounds=(np.log(f0 * 0.7), np.log(f0 * 1.4)),
                          method='bounded')
    f = float(np.exp(res.x))
    rms, rvec, tvec = rms_of(f)
    rms0, rvec0, tvec0 = rms_of(f0)
    if rms0 < rms:
        return f0, rms0, rvec0, tvec0
    return f, rms, rvec, tvec


def analyse(path, verbose=True):
    img = cv2.imread(path)
    if img is None:
        raise RuntimeError("khong doc duoc %s" % path)
    H, W = img.shape[:2]
    wm = white_mask(img)

    (al, bl, sl), (ar, br_, sr), left, right, y_top, y_bot = find_posts(img, wm)
    y_cb, y_base = find_crossbar_and_base(img, left, right, y_top, y_bot)
    xl_top, xl_bot = al * y_cb + bl, al * y_base + bl
    xr_top, xr_bot = ar * y_cb + br_, ar * y_base + br_

    peaks = ridge_peaks(ridge_profile(img), int(y_base) - 4, H - 12)
    y_l0 = min(peaks, key=lambda p: abs(p - y_base)) if peaks else y_base
    cands = [p for p in peaks if p > y_l0 + 20]
    if len(cands) < 2:
        raise RuntimeError("chi thay %d vach voi duoi vach cau mon" % len(cands))

    bx, by, br = find_ball(img, wm, (int(y_l0) + 30, int(cands[-1]) - 10), (700, 1220))
    y_contact = by + br

    # Hieu chinh chi bang cac moc TINH va sac net: 4 goc khung thanh + 2 vach
    # voi. Qua bong KHONG tham gia, de danh lam phep kiem chung doc lap.
    world = np.array([
        [-GOAL_HALF_W, GOAL_H, 0.0],
        [GOAL_HALF_W, GOAL_H, 0.0],
        [GOAL_HALF_W, 0.0, 0.0],
        [-GOAL_HALF_W, 0.0, 0.0],
        [0.0, 0.0, SIX_YARD],
        [0.0, 0.0, PEN_AREA],
    ])
    xmid = (xl_bot + xr_bot) / 2.0

    def build(p5, p16):
        return np.array([
            [xl_top, y_cb], [xr_top, y_cb], [xr_bot, y_base], [xl_bot, y_base],
            [xmid, p5], [xmid, p16],
        ])

    # Cac dinh ridge co the la bong do hay vet san -> thu moi cach gan vach nao
    # la 5m50 / 16m50, chon cach vua hop ly vat ly vua khop qua bong nhat.
    best = None
    for i, p5 in enumerate(cands):
        for p16 in cands[i + 1:]:
            if p16 - p5 < 40:
                continue
            imgp = build(p5, p16)
            f_, rms_, rv_, tv_ = solve_f(world, imgp, W, H)
            R_, _ = cv2.Rodrigues(rv_)
            C_ = (-R_.T @ tv_).ravel()
            if not (1.0 < C_[1] < 6.0 and 18.0 < C_[2] < 40.0):
                continue
            K_ = np.array([[f_, 0, W / 2.0], [0, f_, H / 2.0], [0, 0, 1.0]])
            pb, _ = cv2.projectPoints(np.array([[0.0, BALL_R, PEN_DIST]]), rv_, tv_, K_, None)
            derr = float(np.hypot(*(pb.ravel() - [bx, by])))
            if derr > 15.0:
                continue
            score = rms_ + 0.1 * derr
            if best is None or score < best[0]:
                best = (score, rms_, f_, rv_, tv_, imgp, p5, p16, pb.ravel(), derr)
    if best is None:
        raise RuntimeError("khong cach gan vach nao cho ket qua hop ly")
    _, rms, f, rvec, tvec, image, y_l5, y_l16, ball_pred, ball_err = best
    R, _ = cv2.Rodrigues(rvec)
    C = (-R.T @ tvec).ravel()

    if verbose:
        print("== %s ==" % path)
        print("  mep trong tren : x=%.2f .. %.2f (rong %.2f px)" % (xl_top, xr_top, xr_top - xl_top))
        print("  mep trong duoi : x=%.2f .. %.2f (rong %.2f px)" % (xl_bot, xr_bot, xr_bot - xl_bot))
        print("  do lech khop cot: trai %.2f px, phai %.2f px" % (sl, sr))
        print("  xa ngang duoi y=%.2f  chan cot y=%.2f  (cao %.2f px)" % (y_cb, y_base, y_base - y_cb))
        print("  vach voi phat hien: %s" % ", ".join("%.2f" % p for p in peaks))
        print("  -> cau mon y=%.2f  5m50 y=%.2f  16m50 y=%.2f" % (y_l0, y_l5, y_l16))
        print("  KIEM CHUNG bong: do duoc (%.2f, %.2f) r=%.2f | du doan cham pen "
              "(%.2f, %.2f) | lech %.2f px = %.1f cm"
              % (bx, by, br, ball_pred[0], ball_pred[1], ball_err,
                 100.0 * ball_err / ((xr_bot - xl_bot) / 7.32) * (C[2] - PEN_DIST) / C[2]))
        print("  ty le tai z=0: ngang %.2f px/m, doc %.2f px/m"
              % ((xr_bot - xl_bot) / 7.32, (y_base - y_cb) / GOAL_H))
        print("  --> f=%.1f px  RMS=%.3f px  FOVx=%.1f deg"
              % (f, rms, 2 * np.degrees(np.arctan(W / 2 / f))))
        print("  --> camera (x,y,z) = %.2f, %.2f, %.2f  (cao %.2f m, sau cham pen %.2f m)"
              % (C[0], C[1], C[2], C[1], C[2] - PEN_DIST))
        K = np.array([[f, 0, W / 2.0], [0, f, H / 2.0], [0, 0, 1.0]])
        proj, _ = cv2.projectPoints(world, rvec, tvec, K, None)
        for w_, i_, p_ in zip(world, image, proj.reshape(-1, 2)):
            print("     %-24s d=(%+5.2f, %+5.2f)" % (np.array2string(w_, precision=2),
                                                     p_[0] - i_[0], p_[1] - i_[1]))
    return dict(points=image.tolist(), world=world.tolist(), focal=f, rms=rms,
                ball=[bx, by, br], W=W, H=H)


if __name__ == "__main__":
    for p in sys.argv[1:]:
        analyse(p)
        print()
