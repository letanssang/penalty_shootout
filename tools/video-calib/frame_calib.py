#!/usr/bin/env python3
"""Hieu chinh camera cho TUNG khung hinh (camera eFootball khong dung yen)."""
import cv2
import numpy as np
import auto_calib as ac

W_, H_ = 1920, 1080


def measure(img):
    """Do cac moc tinh trong 1 khung hinh. Tra ve dict, thieu gi thi None."""
    wm = ac.white_mask(img)
    (al, bl, sl), (ar, br_, sr), left, right, y_top, y_bot = ac.find_posts(img, wm)
    y_cb, y_base = ac.find_crossbar_and_base(img, left, right, y_top, y_bot)
    out = dict(xl_top=al * y_cb + bl, xl_bot=al * y_base + bl,
               xr_top=ar * y_cb + br_, xr_bot=ar * y_base + br_,
               y_cb=y_cb, y_base=y_base, res_l=sl, res_r=sr)
    peaks = ac.ridge_peaks(ac.ridge_profile(img), int(y_base) - 4, H_ - 12)
    y_l0 = min(peaks, key=lambda p: abs(p - y_base)) if peaks else y_base
    out['lines'] = [p for p in peaks if p > y_l0 + 20]
    return out


GOAL4 = np.array([
    [-ac.GOAL_HALF_W, ac.GOAL_H, 0.0], [ac.GOAL_HALF_W, ac.GOAL_H, 0.0],
    [ac.GOAL_HALF_W, 0.0, 0.0], [-ac.GOAL_HALF_W, 0.0, 0.0]])


def goal_img(m):
    return np.array([[m['xl_top'], m['y_cb']], [m['xr_top'], m['y_cb']],
                     [m['xr_bot'], m['y_base']], [m['xl_bot'], m['y_base']]])


def solve6(m):
    """Giai day du (f + tu the) khi co du 2 vach voi."""
    xmid = (m['xl_bot'] + m['xr_bot']) / 2.0
    cands = m['lines']
    best = None
    for i, p5 in enumerate(cands):
        for p16 in cands[i + 1:]:
            if p16 - p5 < 30:
                continue
            world = np.vstack([GOAL4, [[0, 0, ac.SIX_YARD], [0, 0, ac.PEN_AREA]]])
            imgp = np.vstack([goal_img(m), [[xmid, p5], [xmid, p16]]])
            f, rms, rv, tv = ac.solve_f(world, imgp, W_, H_)
            R, _ = cv2.Rodrigues(rv)
            C = (-R.T @ tv).ravel()
            if not (1.0 < C[1] < 6.0 and 12.0 < C[2] < 45.0 and abs(C[0]) < 3.0):
                continue
            if best is None or rms < best[0]:
                best = (rms, f, rv, tv, C)
    return best


def pose_fixed_f(m, f):
    """Chi giai tu the khi da biet f (4 goc khung thanh la du)."""
    K = np.array([[f, 0, W_ / 2.0], [0, f, H_ / 2.0], [0, 0, 1.0]])
    ok, rv, tv = cv2.solvePnP(GOAL4, goal_img(m), K, None, flags=cv2.SOLVEPNP_ITERATIVE)
    proj, _ = cv2.projectPoints(GOAL4, rv, tv, K, None)
    d = proj.reshape(-1, 2) - goal_img(m)
    rms = float(np.sqrt((d ** 2).sum(axis=1).mean()))
    R, _ = cv2.Rodrigues(rv)
    return rms, rv, tv, (-R.T @ tv).ravel()
