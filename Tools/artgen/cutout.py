import sys, numpy as np, cv2
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
im = np.array(Image.open(src).convert('RGB')).astype(np.int16)
mn, mx = im.min(axis=2), im.max(axis=2)
cand = ((mn > 196) & ((mx - mn) < 16)).astype(np.uint8)        # açık gri/beyaz, renksiz
n, lab = cv2.connectedComponents(cand, connectivity=4)
border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
bg = np.isin(lab, list(border)).astype(np.uint8)
# küçük delikleri kapat, kenardaki açık hale halkasını da arka plana kat
bg = cv2.morphologyEx(bg, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
halo = cv2.dilate(bg, np.ones((3, 3), np.uint8)) & cand
bg = np.maximum(bg, halo)
fg = 1 - bg
# en büyük nesne parçası dışındaki kırıntıları at
n2, lab2, st, _ = cv2.connectedComponentsWithStats(fg.astype(np.uint8), connectivity=8)
keep = np.zeros_like(fg)
for i in range(1, n2):
    if st[i, cv2.CC_STAT_AREA] > 400: keep[lab2 == i] = 1
alpha = cv2.GaussianBlur((keep * 255).astype(np.uint8), (3, 3), 0)
alpha = np.minimum(alpha, (keep * 255).astype(np.uint8) | cv2.erode(alpha, np.ones((2, 2), np.uint8)))
out = np.dstack([im.astype(np.uint8), alpha])
ys, xs = np.where(keep > 0)
print('bbox', xs.min(), ys.min(), xs.max(), ys.max(), 'fg%', round(keep.mean() * 100, 1))
Image.fromarray(out, 'RGBA').save(dst)
