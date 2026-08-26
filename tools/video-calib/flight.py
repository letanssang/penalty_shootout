"""Tim mo bong LUC DANG BAY mot cach tu dong, roi bam ca hai chieu."""
import cv2, numpy as np, ball_track as bt


def cands(bn, r0, lo=0.55, hi=2.2):
    """Moi dom tron 'khong phai co' co ban kinh hop ly tren ca khung hinh."""
    m = (bn > -30).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3,3), np.uint8))
    n, lab, st, ce = cv2.connectedComponentsWithStats(m, 8)
    out = []
    for k in range(1, n):
        w, h, a = st[k][2], st[k][3], st[k][4]
        rr = 0.25 * (w + h)
        if not (lo*r0 < rr < hi*r0): continue
        if max(w,h) > 1.9*min(w,h): continue
        if a < 0.45*np.pi*rr**2 or a > 1.7*np.pi*rr**2: continue
        out.append((ce[k][0], ce[k][1], rr))
    return out


def find_seed(load, renders, r0, exclude, verbose=False):
    """Ba khung lien tiep co dom di chuyen THANG DEU -> do la qua bong."""
    pool = {}
    for i in renders:
        bn = bt.ballness(load(i))
        good = []
        for c in cands(bn, r0):
            if np.hypot(c[0]-exclude[0], c[1]-exclude[1]) < 25: continue
            o = bt.refine_radial(bn, c[0], c[1], c[2])
            if o is None or o[3] > 1.0: continue
            if not (0.55*r0 < o[2] < 2.2*r0): continue
            good.append(o)
        pool[i] = good
    best = None
    ks = list(renders)
    for a in range(len(ks)-2):
        for A in pool[ks[a]]:
            for B in pool[ks[a+1]]:
                v = np.hypot(B[0]-A[0], B[1]-A[1])
                if not (12 < v < 260): continue
                px, py = 2*B[0]-A[0], 2*B[1]-A[1]
                for C in pool[ks[a+2]]:
                    e = np.hypot(C[0]-px, C[1]-py)
                    if e > 0.30*v: continue
                    sc = e/max(v,1) + 0.3*(A[3]+B[3]+C[3])
                    if best is None or sc < best[0]:
                        best = (sc, ks[a+1], B, v, e)
    if verbose and best:
        print('    mo bay: khung %d (%.1f, %.1f) r=%.2f  toc %.1f px/render  sai lech %.2f'
              % (best[1], best[2][0], best[2][1], best[2][2], best[3], best[4]))
    return (best[1], best[2]) if best else (None, None)
