#!/usr/bin/env python3
"""Do tam va ban kinh qua bong duoi pixel bang khop duong tron theo gradient.

Bong la dia sang tren nen toi hon, nen o mep bien do sang giam khi di ra ngoai
-> vector gradient chi VAO tam. Cac hoa tiet toi ben trong bong cung tao
gradient manh nhung huong lung tung, nen loc theo huong xuyen tam se bo duoc.
"""
import cv2
import numpy as np


def coarse_blob(img, bg, box, dmin=70):
    """Tim dom sang di chuyen trong khung tim kiem box=(x0,y0,x1,y1)."""
    x0, y0, x1, y1 = box
    sub = img[y0:y1, x0:x1].astype(np.int16)
    d = np.abs(sub - bg[y0:y1, x0:x1]).sum(axis=2)
    hsv = cv2.cvtColor(img[y0:y1, x0:x1], cv2.COLOR_BGR2HSV)
    _, S, V = cv2.split(hsv)
    m = ((d > dmin) & (V > 140) & (S < 80)).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    n, lab, st, ce = cv2.connectedComponentsWithStats(m, 8)
    best = None
    for k in range(1, n):
        w, h, a = st[k][2], st[k][3], st[k][4]
        if a < 25 or w > 80 or h > 80:
            continue
        if max(w, h) > 2.2 * min(w, h):     # bong gan tron
            continue
        if best is None or a > st[best][4]:
            best = k
    if best is None:
        return None
    w, h = st[best][2], st[best][3]
    return (x0 + ce[best][0], y0 + ce[best][1], 0.25 * (w + h))


def ballness(img):
    """Do 'khong-phai-co' cua tung diem anh.

    Co co thanh phan luc troi hon han; qua bong trang/xam co R=G=B nen hieu nay
    ~0 du bong bi che toi. Nho vay mep bong van do duoc o phia khuat sang, noi
    ma gradient do sang gan nhu bien mat.
    """
    b, g, r = cv2.split(img.astype(np.float32))
    return 0.5 * (b + r) - g


def refine_radial(bn, cx, cy, r, nray=96, iters=4):
    """Khop duong tron vao mep 'ballness' bang cach do doc theo tia xuyen tam."""
    H, W = bn.shape
    for _ in range(iters):
        yy, xx = np.mgrid[max(int(cy - 2 * r), 0):min(int(cy + 2 * r) + 1, H),
                          max(int(cx - 2 * r), 0):min(int(cx + 2 * r) + 1, W)]
        rad = np.hypot(xx - cx, yy - cy)
        pin = bn[yy, xx][rad < 0.55 * r]
        pout = bn[yy, xx][(rad > 1.45 * r) & (rad < 1.9 * r)]
        if pin.size < 20 or pout.size < 40:
            return None
        lo, hi = float(np.median(pout)), float(np.median(pin))
        if hi - lo < 25:
            return None
        half = 0.5 * (lo + hi)

        ang = np.arange(nray) * (2 * np.pi / nray)
        rs = np.arange(0.45 * r, 1.75 * r, 0.05)
        px = (cx + np.cos(ang)[:, None] * rs[None, :]).astype(np.float32)
        py = (cy + np.sin(ang)[:, None] * rs[None, :]).astype(np.float32)
        prof = cv2.remap(bn, px, py, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
        ex, ey = [], []
        for k in range(nray):
            p = prof[k]
            idx = np.nonzero((p[:-1] >= half) & (p[1:] < half))[0]
            if idx.size == 0:
                continue
            j = int(idx[0])
            t = (p[j] - half) / (p[j] - p[j + 1])
            rr = rs[j] + t * (rs[j + 1] - rs[j])
            ex.append(cx + np.cos(ang[k]) * rr)
            ey.append(cy + np.sin(ang[k]) * rr)
        if len(ex) < nray * 0.45:
            return None
        ex, ey = np.array(ex), np.array(ey)
        w = np.ones_like(ex)
        for _ in range(4):
            A = np.stack([ex, ey, np.ones_like(ex)], axis=1) * w[:, None]
            sol, *_ = np.linalg.lstsq(A, (ex ** 2 + ey ** 2) * w, rcond=None)
            ncx, ncy = sol[0] / 2.0, sol[1] / 2.0
            nr = np.sqrt(max(sol[2] + ncx ** 2 + ncy ** 2, 1e-6))
            res = np.hypot(ex - ncx, ey - ncy) - nr
            s = max(1.4826 * np.median(np.abs(res)), 0.15)
            u = np.clip(res / (3.0 * s), -1, 1)
            w = (1 - u ** 2) ** 2
        if not np.isfinite(nr) or nr < 3 or nr > 60:
            return None
        moved = np.hypot(ncx - cx, ncy - cy) + abs(nr - r)
        cx, cy, r = float(ncx), float(ncy), float(nr)
        if moved < 0.02:
            break
    good = w > 0.05
    rms = float(np.sqrt((res[good] ** 2).mean()))
    return cx, cy, r, rms, int(good.sum())


def blob_candidates(bn, cx, cy, gate, r_hint):
    """Tim cac dom 'khong phai co' gon trong vong gate quanh (cx, cy).

    Co co bn rat am, bong ~0, nen nguong -30 tach duoc bong khoi san. Tra ve
    danh sach (x, y, r) sap theo khoang cach toi tam gate.
    """
    H, W = bn.shape
    x0, x1 = max(int(cx - gate), 0), min(int(cx + gate) + 1, W)
    y0, y1 = max(int(cy - gate), 0), min(int(cy + gate) + 1, H)
    if x1 - x0 < 8 or y1 - y0 < 8:
        return []
    m = (bn[y0:y1, x0:x1] > -30).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, st, ce = cv2.connectedComponentsWithStats(m, 8)
    out = []
    for k in range(1, n):
        w, h, a = st[k][2], st[k][3], st[k][4]
        if a < 0.4 * np.pi * r_hint ** 2 or a > 6 * np.pi * r_hint ** 2:
            continue
        if max(w, h) > 2.4 * min(w, h) or max(w, h) > 5 * r_hint:
            continue
        out.append((x0 + ce[k][0], y0 + ce[k][1], 0.25 * (w + h)))
    out.sort(key=lambda p: np.hypot(p[0] - cx, p[1] - cy))
    return out


def distinct_frames(load, lo, hi, thr=3.0):
    """Chi so cac khung THAT SU moi (bo khung lap do capture 60fps > render)."""
    step = 1 if hi >= lo else -1
    out = [lo]
    prev = load(lo).astype(np.int16)
    for i in range(lo + step, hi + step, step):
        cur = load(i).astype(np.int16)
        if np.abs(cur - prev).mean() > thr:
            out.append(i)
            prev = cur
    return out


def track(frames, load, seed, rms_max=1.2, gate_min=45.0, verbose=False):
    """Bam qua bong qua danh sach khung PHAN BIET (khong con khung lap).

    seed = (chi so khung, cx, cy, r). Du doan bang van toc khong doi; cong tim
    kiem lay theo van toc that gan nhat, khong bao gio nho hon gate_min.
    """
    hist, out = [], {}
    speed = gate_min
    i0 = frames.index(seed[0])
    guess = (float(seed[1]), float(seed[2]), float(seed[3]))
    for i in frames[i0:]:
        if len(hist) >= 2:
            (ax, ay, ar), (bx, by, br) = hist[-2], hist[-1]
            v = np.hypot(bx - ax, by - ay)
            if v > 1.0:
                speed = v
            guess = (2 * bx - ax, 2 * by - ay, max(2 * br - ar, 4.0))
        elif hist:
            guess = hist[-1]
        gate = max(1.6 * speed, gate_min)
        bn = ballness(load(i))

        starts = [guess]
        for dx, dy in [(-7, 0), (7, 0), (0, -7), (0, 7), (-13, -9), (13, 9)]:
            starts.append((guess[0] + dx, guess[1] + dy, guess[2]))
        for bx_, by_, br_ in blob_candidates(bn, guess[0], guess[1], gate, guess[2])[:5]:
            starts.append((bx_, by_, max(br_, 4.0)))

        best = None
        for s in starts:
            o = refine_radial(bn, s[0], s[1], s[2])
            if o is None or o[3] > rms_max:
                continue
            if np.hypot(o[0] - guess[0], o[1] - guess[1]) > gate:
                continue
            if best is None or o[3] < best[3]:
                best = o
        if best is None:
            if verbose:
                print("  khung %d: mat dau" % i)
            if out:
                break
            continue
        out[i] = best
        hist.append((best[0], best[1], best[2]))
    return out


def refine(gray, cx, cy, r, iters=4):
    """Tinh chinh (cx, cy, r) bang khop duong tron co trong so gradient."""
    for _ in range(iters):
        pad = int(r * 2.2) + 6
        xa, xb = int(cx) - pad, int(cx) + pad
        ya, yb = int(cy) - pad, int(cy) + pad
        if xa < 1 or ya < 1 or xb >= gray.shape[1] - 1 or yb >= gray.shape[0] - 1:
            return None
        win = gray[ya:yb, xa:xb].astype(np.float32)
        gx = cv2.Sobel(win, cv2.CV_32F, 1, 0, ksize=3)
        gy = cv2.Sobel(win, cv2.CV_32F, 0, 1, ksize=3)
        mag = np.hypot(gx, gy)
        yy, xx = np.mgrid[ya:yb, xa:xb]
        dx, dy = xx - cx, yy - cy
        rad = np.hypot(dx, dy)
        ok = (rad > r * 0.55) & (rad < r * 1.5) & (mag > 25)
        if ok.sum() < 30:
            return None
        # gradient phai chi vao tam: cos(goc) giua -grad va huong xuyen tam
        cosang = -(gx * dx + gy * dy) / np.maximum(mag * rad, 1e-6)
        ok &= cosang > 0.80
        if ok.sum() < 25:
            return None
        px, py, w = xx[ok].astype(np.float64), yy[ok].astype(np.float64), mag[ok].astype(np.float64)
        # khop dai so Kasa co trong so, lap lai voi trong so Tukey
        for _ in range(3):
            A = np.stack([px, py, np.ones_like(px)], axis=1) * w[:, None]
            b = (px ** 2 + py ** 2) * w
            sol, *_ = np.linalg.lstsq(A, b, rcond=None)
            ncx, ncy = sol[0] / 2.0, sol[1] / 2.0
            nr = np.sqrt(max(sol[2] + ncx ** 2 + ncy ** 2, 1e-6))
            res = np.hypot(px - ncx, py - ncy) - nr
            s = max(1.4826 * np.median(np.abs(res)), 0.25)
            u = np.clip(res / (4.0 * s), -1, 1)
            w = mag[ok].astype(np.float64) * (1 - u ** 2) ** 2
        if not np.isfinite(nr) or nr < 3 or nr > 60:
            return None
        moved = np.hypot(ncx - cx, ncy - cy) + abs(nr - r)
        cx, cy, r = ncx, ncy, nr
        rms = float(np.sqrt(np.average(res ** 2, weights=np.maximum(w, 1e-9))))
        npts = int(ok.sum())
        if moved < 0.02:
            break
    return cx, cy, r, rms, npts
