"""Dung lai quy dao 3D voi tu the rang buoc ray + ban kinh bong do duoc."""
import cv2, numpy as np, auto_calib as ac, frame_calib as fc, railpose as rp

W_, H_, F = 1920, 1080, 2861.5
R_BALL = 0.10984
K = np.array([[F,0,W_/2.],[0,F,H_/2.],[0,0,1.]]); Ki = np.linalg.inv(K)
TICK = {73:-5, 75:-4, 76:-3, 78:-2, 79:-1, 80:0, 82:1, 83:3, 84:4, 86:5,
        87:6, 89:7, 90:8, 91:10, 93:11, 94:12, 95:13, 97:14}
DUP = {74:73, 77:76}
tr = {int(r[0]): r[1:] for r in np.load('track1.npy')}

# --- tu the tren moi khung do duoc ---
pose = {}
for i in range(52, 102):
    try: m = fc.measure(cv2.imread('fr1/%04d.jpg' % i))
    except Exception: continue
    rms, z, pan, tilt = rp.solve_rail(m, F)
    pose[i] = (rms, z, pan, tilt)
bad = [i for i in pose if pose[i][0] > 1.0]
print('khung giai duoc tu the: %d (52-101), rms>1px: %s' % (len(pose), bad))
a = np.array([[i, *pose[i]] for i in sorted(pose)])
print('rms tu the: trung binh %.3f px, lon nhat %.3f px' % (a[:,1].mean(), a[:,1].max()))

# --- lam tron duong chay camera theo thoi gian tick ---
ticks_all, vals = [], []
for i in sorted(pose):
    key = DUP.get(i, i)
    if key in TICK and pose[i][0] < 1.0:
        ticks_all.append(TICK[key]); vals.append(pose[i][1:])
ticks_all, vals = np.array(ticks_all, float), np.array(vals)
A = np.stack([ticks_all**k for k in range(3)], axis=1)
coef, *_ = np.linalg.lstsq(A, vals, rcond=None)
fitres = vals - A @ coef
print('khop duong chay camera: sai lech lon nhat  z=%.4f m  ngang=%.5f do  ngang=%.5f do'
      % (np.abs(fitres[:,0]).max(), np.degrees(np.abs(fitres[:,1]).max()),
         np.degrees(np.abs(fitres[:,2]).max())))

def cam_at(tick):
    v = np.array([tick**k for k in range(3)]) @ coef
    return v[0], v[1], v[2]

# --- dung lai quy dao ---
rows = []
for i in sorted(tr):
    key = DUP.get(i, i)
    if key not in TICK: continue
    n = TICK[key]
    z, pan, tilt = cam_at(n)
    Rm, t = rp.extrinsics(z, pan, tilt)
    C = np.array([0.0, rp.Y0, z])
    cx, cy, r = tr[i][0], tr[i][1], tr[i][2]
    u = Ki @ np.array([cx, cy, 1.0])
    u = (Rm.T @ u.reshape(3,1)).ravel(); u /= np.linalg.norm(u)
    d = F * R_BALL / r
    P = C + d * u
    rows.append((n, n*0.02, P[0], P[1], P[2], d, i))
rows.sort()
print('\n%5s %7s %9s %9s %9s %9s %6s' % ('tick','t(s)','x','y','z','dosau','khung'))
for n, t, x, y, zz, d, i in rows:
    print('%5d %7.3f %9.4f %9.4f %9.4f %9.4f %6d' % (n, t, x, y, zz, d, i))

P = np.array([[t, x, y, z] for n, t, x, y, z, d, i in rows])
np.save('traj2.npy', P)

def fit(col, name, deg=2):
    t = P[:,0]
    A = np.stack([t**k for k in range(deg+1)], axis=1)
    c, *_ = np.linalg.lstsq(A, P[:,col], rcond=None)
    r = P[:,col] - A @ c
    print('%s: %s  rms %.4f  max %.4f' % (name,
          '  '.join('c%d=%+.4f' % (k, v) for k, v in enumerate(c)),
          np.sqrt((r**2).mean()), np.abs(r).max()))
    return c

print()
cy_ = fit(2, 'y(t)')
print('   -> g = %.4f m/s^2   (that: 9.81, lech %+.2f%%)' % (-2*cy_[2], (-2*cy_[2]/9.81-1)*100))
fit(1, 'x(t)')
fit(3, 'z(t)')
