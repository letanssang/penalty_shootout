"""Do gi CHAC va do gi KHONG chac: sai so chuan cua tung he so."""
import numpy as np
P = np.load('traj2.npy'); P[:,0] -= P[0,0]
t = P[:,0]; A = np.stack([t**k for k in range(3)], axis=1)
lab = ['ngang x', 'doc  y', 'sau  z']
print('%-8s %10s %14s %16s' % ('truc', 'v0 (m/s)', 'gia toc (m/s2)', 'sai so gia toc'))
for c in (1, 2, 3):
    y = P[:, c]
    coef, *_ = np.linalg.lstsq(A, y, rcond=None)
    r = y - A @ coef
    s2 = (r**2).sum() / (len(t) - 3)
    cov = s2 * np.linalg.inv(A.T @ A)
    se = np.sqrt(np.diag(cov))
    print('%-8s %7.2f+-%.2f %9.2f+-%.2f %13s' % (lab[c-1], coef[1], se[1],
          2*coef[2], 2*se[2], '%.0f%% cua tri so' % abs(200*se[2]/max(abs(coef[2]),1e-9))))
    if c == 2:
        print('         -> g = %.2f +- %.2f m/s2  (that 9.81)' % (-2*coef[2], 2*se[2]))
sp = np.hypot(np.linalg.lstsq(A, P[:,1], rcond=None)[0][1],
              np.linalg.lstsq(A, P[:,3], rcond=None)[0][1])
print('\ntoc do dau |v0| = %.2f m/s = %.0f km/h' % (sp, sp*3.6))
print('luc can du kien (Cd=0.25) o %.0f m/s: %.1f m/s2 -> nam GON trong sai so tren'
      % (sp, 0.5*1.225*0.25*np.pi*0.10984**2*sp**2/0.43))
