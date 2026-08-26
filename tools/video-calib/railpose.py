"""Tu the camera rang buoc 'chay tren ray': x=0, cao=Y0 co dinh.

4 goc khung thanh cho 8 rang buoc, chi con 3 an (z, ngang, ngang) -> goc ngang
tro nen quan sat duoc, khac han solvePnP 6 bac tu do von suy bien.
"""
import cv2, numpy as np
from scipy.optimize import least_squares
import auto_calib as ac, frame_calib as fc

W_, H_ = 1920, 1080
Y0 = 2.8829                      # cao camera, tu 8 khung giai 6 diem


def rot_world(pan, tilt, roll=0.0):
    """Ma tran camera->the gioi tu 3 goc (radian)."""
    fwd = np.array([np.sin(pan)*np.cos(tilt), np.sin(tilt), -np.cos(pan)*np.cos(tilt)])
    right = np.array([np.cos(pan), 0.0, np.sin(pan)])
    down = np.cross(fwd, right)
    if roll:
        c, s = np.cos(roll), np.sin(roll)
        right, down = c*right + s*down, -s*right + c*down
    return np.stack([right, down, fwd], axis=1)


def extrinsics(z, pan, tilt, roll=0.0, y0=Y0):
    Rw = rot_world(pan, tilt, roll)
    R = Rw.T
    C = np.array([0.0, y0, z])
    return R, (-R @ C).reshape(3, 1)


def project(P, R, t, K):
    q = (R @ P.T + t).T
    return (q[:, :2] / q[:, 2:3]) * np.array([K[0,0], K[1,1]]) + np.array([K[0,2], K[1,2]])


def solve_rail(m, f, y0=Y0, extra=None, x0=(25.0, 0.0, -0.12)):
    """extra = danh sach (diem_the_gioi, hang_anh) bo sung (vd vach san)."""
    K = np.array([[f,0,W_/2.],[0,f,H_/2.],[0,0,1.]])
    obs = fc.goal_img(m)
    xmid = (m['xl_bot'] + m['xr_bot']) / 2.0

    def res(p):
        R, t = extrinsics(p[0], p[1], p[2], y0=y0)
        r = (project(fc.GOAL4, R, t, K) - obs).ravel()
        if extra:
            for Pw, row in extra:
                q = project(np.array([Pw]), R, t, K)[0]
                r = np.concatenate([r, [q[1] - row, (q[0] - xmid) * 0.1]])
        return r

    s = least_squares(res, np.array(x0), method='lm', xtol=1e-12, ftol=1e-12)
    rms = float(np.sqrt((s.fun ** 2).mean() * 2))     # px (2 thanh phan/diem)
    return rms, s.x[0], s.x[1], s.x[2]


if __name__ == '__main__':
    import pose6 as p6
    print('%5s | %-26s | %-26s | %s' % ('khung', 'rang buoc ray (z, ngang, ngang)',
                                        'day du 6 diem', 'chenh'))
    for i in range(52, 60):
        m = fc.measure(cv2.imread('fr1/%04d.jpg' % i))
        rms, z, pan, tilt = solve_rail(m, 2861.5)
        rms6, rv, tv, C = p6.pose6(m)
        R6, _ = cv2.Rodrigues(rv); Rw6 = R6.T
        t6 = np.degrees(np.arcsin(Rw6[1, 2]))
        print('%5d | z=%7.4f ng=%+.4f ngang=%+.4f rms=%.2f | z=%7.4f ngang=%+.4f rms=%.2f | dz=%+.4f dt=%+.4f'
              % (i, z, np.degrees(pan), np.degrees(tilt), rms, C[2], t6, rms6, z-C[2], np.degrees(tilt)-t6))
