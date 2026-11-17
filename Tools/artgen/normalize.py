"""Kesilmiş bina görselini standart tuvale yerleştirir: 512x768, zemin plakasının ortası (256, 600), plaka genişliği 420 px."""
import sys, numpy as np
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGBA'); a = np.array(im)[:, :, 3]
ys, xs = np.where(a > 128)
minx, maxx = xs.min(), xs.max()
ly = np.median(ys[xs <= minx + 3]); ry = np.median(ys[xs >= maxx - 3])
cx, cy, pw = (minx + maxx) / 2, (ly + ry) / 2, maxx - minx
k = 420.0 / pw
W, H, CX, CY = 512, 768, 256, 600
top_needed = (cy - ys.min()) * k
if top_needed > CY - 4: k *= (CY - 4) / top_needed; print('uyarı: yüksek bina, küçültüldü')
im2 = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
out.alpha_composite(im2, (round(CX - cx * k), round(CY - cy * k)))
out.save(dst, optimize=True)
print('plaka merkezi', round(cx), round(cy), 'genişlik', pw, 'ölçek', round(k, 3))
