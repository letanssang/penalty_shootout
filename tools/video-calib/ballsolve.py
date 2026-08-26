"""Bong tinh nam TREN MAT DAT -> giai duoc ca ban kinh that lan vi tri.

An: d (do sau doc tia) va R (ban kinh that). Rang buoc:
  P_y = R           (day bong cham dat)
  R   = r_px * d / f  (ban kinh bieu kien)
=> d = C_y / (r_px/f - u_y).  Khong can gia dinh cham phat den o 11.0 m.
"""
import cv2, numpy as np, auto_calib as ac, frame_calib as fc, railpose as rp
W_, H_ = 1920, 1080
K0 = lambda f: np.array([[f,0,W_/2.],[0,f,H_/2.],[0,0,1.]])
tr = {int(r[0]): r[1:] for r in np.load('track1.npy')}

def run(f, y0):
    K = K0(f); Ki = np.linalg.inv(K)
    rows = []
    for i in range(52, 72):
        if i not in tr: continue
        try: m = fc.measure(cv2.imread('fr1/%04d.jpg' % i))
        except Exception: continue
        cx, cy, r = tr[i][0], tr[i][1], tr[i][2]
        rms, z, pan, tilt = rp.solve_rail(m, f, y0=y0)
        R, t = rp.extrinsics(z, pan, tilt, y0=y0)
        C = np.array([0.0, y0, z])
        u = Ki @ np.array([cx, cy, 1.0])
        u = (R.T @ u.reshape(3,1)).ravel(); u /= np.linalg.norm(u)
        d = y0 / (r / f - u[1])
        P = C + d * u
        rows.append((i, P[0], P[1], P[2], d, r, z))
    return np.array(rows)

a = run(2861.5, rp.Y0)
print('%5s %9s %9s %9s %9s %8s %9s' % ('khung','bong_x','bong_y=R','bong_z','dosau','r_px','cam_z'))
for row in a:
    print('%5d %9.4f %9.4f %9.4f %9.4f %8.3f %9.4f' % tuple(row))
print('\nban kinh that R = %.5f +- %.5f m  (chu vi %.1f cm)'
      % (a[:,2].mean(), a[:,2].std(), 2*np.pi*a[:,2].mean()*100))
print('vi tri bong   z = %.4f +- %.4f m   x = %.4f +- %.4f m'
      % (a[:,3].mean(), a[:,3].std(), a[:,1].mean(), a[:,1].std()))
print('cham phat den chuan 11.00 m -> chenh %+.4f m' % (a[:,3].mean() - 11.0))
