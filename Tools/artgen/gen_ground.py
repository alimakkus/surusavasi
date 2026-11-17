"""Yuvarlak köy zemini: 800x1200 oyun pikseli, 2x çözünürlük. Düzen BaseMapView.cs (Layout) ile aynı olmalı.
Kullanım: python3 gen_ground.py <Assets/Resources/Art klasörü>"""
import sys, math, numpy as np
from PIL import Image, ImageDraw, ImageFilter
K = 2; GW, GH = 800, 1200; NX, NY = GW * K, GH * K
CX, CY = 400, 600
WRX, WRY = 250, 215          # sur
RRX, RRY = 175, 150          # bina çemberi
ANG = [-67.5, -22.5, 22.5, 67.5, 112.5, 157.5, 202.5, 247.5]  # 8 bina açısı (derece, y aşağı)

THEMES = {
 'green':  dict(out=[(70,122,52),(98,152,66)], inn=[(102,158,70),(126,180,84)], path=[(176,138,90),(150,112,70)], plaza=[(176,172,160),(140,136,126)], deco=[(240,220,90),(255,255,255),(70,110,50),(130,130,120)]),
 'winter': dict(out=[(214,228,240),(244,248,252)], inn=[(226,236,244),(250,252,255)], path=[(176,192,206),(150,166,184)], plaza=[(170,182,196),(132,146,164)], deco=[(190,214,236),(255,255,255),(150,170,190),(120,140,160)]),
 'desert': dict(out=[(214,176,112),(236,204,140)], inn=[(222,190,130),(240,214,160)], path=[(190,146,96),(160,118,74)], plaza=[(206,176,128),(170,140,96)], deco=[(170,130,80),(250,236,200),(120,150,80),(150,120,90)]),
 'autumn': dict(out=[(166,104,46),(196,138,62)], inn=[(150,140,70),(176,160,86)], path=[(150,108,70),(120,84,54)], plaza=[(168,160,148),(130,122,112)], deco=[(220,80,40),(240,180,40),(170,60,40),(120,90,60)]),
 'lava':   dict(out=[(40,32,32),(64,52,50)], inn=[(76,70,66),(98,90,84)], path=[(118,100,88),(90,74,64)], plaza=[(96,92,96),(66,62,68)], deco=[(255,120,30),(255,200,60),(30,24,24),(90,80,80)], cracks=True),
}

def noise(seed, octaves=(8, 24, 64), weights=(.55, .3, .15)):
    rng = np.random.default_rng(seed); acc = np.zeros((NY, NX))
    for o, w in zip(octaves, weights):
        g = rng.random((o + 1, o + 1))
        acc += w * np.array(Image.fromarray((g * 255).astype(np.uint8)).resize((NX, NY), Image.BICUBIC)) / 255.0
    return np.clip(acc, 0, 1)

def lerp(c, t):
    a, b = np.array(c[0], float), np.array(c[1], float)
    return a[None, None, :] * (1 - t[..., None]) + b[None, None, :] * t[..., None]

def mask_draw(fn, blur=0):
    m = Image.new('L', (NX, NY), 0); fn(ImageDraw.Draw(m))
    if blur: m = m.filter(ImageFilter.GaussianBlur(blur))
    return np.array(m) / 255.0

def ell(d, cx, cy, rx, ry, **kw): d.ellipse([(cx - rx) * K, (cy - ry) * K, (cx + rx) * K, (cy + ry) * K], **kw)

def build(theme, out):
    T = THEMES[theme]; seed = abs(hash(theme)) % 1000
    n1, n2 = noise(seed), noise(seed + 7, (32, 96, 200), (.5, .3, .2))
    img = lerp(T['out'], n1 * .75 + n2 * .25)
    inside = mask_draw(lambda d: ell(d, CX, CY, WRX - 4, WRY - 4, fill=255), 6)
    img = img * (1 - inside[..., None]) + lerp(T['inn'], n1 * .5 + n2 * .5) * inside[..., None]

    # yollar: merkezden binalara ve kapıya, bina çemberi
    bpos = [(CX + RRX * math.cos(math.radians(a)), CY + RRY * math.sin(math.radians(a))) for a in ANG]
    def paths(d):
        for (x, y) in bpos: d.line([CX * K, CY * K, x * K, y * K], fill=255, width=13 * K)
        d.line([CX * K, CY * K, CX * K, (CY + WRY + 20) * K], fill=255, width=20 * K)
        ell(d, CX, CY, RRX, RRY, outline=255, width=12 * K)
        for (x, y) in bpos: ell(d, x, y + 4, 52, 28, fill=255)
    pm = mask_draw(paths, 3) * (inside > .2)
    edge = np.clip(mask_draw(paths, 7) - pm, 0, 1) * (inside > .2)
    pcol = lerp(T['path'], noise(seed + 3, (40, 120, 300), (.4, .35, .25)))
    road = mask_draw(lambda d: d.line([CX * K, (CY + WRY) * K, CX * K, GH * K], fill=255, width=22 * K), 3)
    redge = np.clip(mask_draw(lambda d: d.line([CX * K, (CY + WRY) * K, CX * K, GH * K], fill=255, width=30 * K), 6) - road, 0, 1)
    pm = np.maximum(pm, road); edge = np.maximum(edge, redge * (1 - pm))
    img = img * (1 - pm[..., None]) + pcol * pm[..., None]
    img = img * (1 - .35 * edge[..., None])

    # meydan: taş döşeme halkaları
    pl = mask_draw(lambda d: ell(d, CX, CY, 82, 70, fill=255), 2)
    yy, xx = np.mgrid[0:NY, 0:NX] / K
    r = np.sqrt(((xx - CX) / 82) ** 2 + ((yy - CY) / 70) ** 2)
    ang = np.arctan2((yy - CY) / 70, (xx - CX) / 82)
    rings = np.floor(r * 4)
    seg = np.floor((ang + math.pi) / (2 * math.pi) * (10 + rings * 8))
    tile = (np.sin(rings * 12.9 + seg * 78.2) * 43758.5) % 1
    grout = ((np.abs((r * 4) % 1 - .5) > .44) | (np.abs(((ang + math.pi) / (2 * math.pi) * (10 + rings * 8)) % 1 - .5) > .45))
    stone = lerp(T['plaza'], .3 + .5 * tile)
    stone[grout] *= .72
    img = img * (1 - pl[..., None]) + stone * pl[..., None]
    ring = mask_draw(lambda d: ell(d, CX, CY, 84, 72, outline=255, width=4 * K), 1)
    img = img * (1 - .45 * ring[..., None])

    # süsler: dışta çiçek/çakıl/yaprak, içte seyrek ot
    rng = np.random.default_rng(seed + 11)
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)); d = ImageDraw.Draw(im)
    def inside_e(x, y): return ((x - CX) / WRX) ** 2 + ((y - CY) / WRY) ** 2
    pmask = pm
    # ot öbekleri (her yerde, yolların dışında)
    dark = tuple(int(c * .78) for c in T['inn'][0]); light = tuple(min(255, int(c * 1.12)) for c in T['inn'][1])
    odark = tuple(int(c * .78) for c in T['out'][0]); olight = tuple(min(255, int(c * 1.12)) for c in T['out'][1])
    for i in range(5200):
        x, y = rng.random() * GW, rng.random() * GH
        if pmask[int(y * K) % NY, int(x * K) % NX] > .3 or pl[int(y * K) % NY, int(x * K) % NX] > .1: continue
        ins = inside_e(x, y) < .97
        c1, c2 = (dark, light) if ins else (odark, olight)
        for j in range(3):
            dx = (j - 1) * 1.3; h = 2.5 + rng.random() * 2
            d.line([(x + dx) * K, y * K, (x + dx * 1.8) * K, (y - h) * K], fill=c1 if j != 1 else c2, width=2)
    # çiçek kümeleri ve taşlar (sur dışında yoğun, içte seyrek)
    for i in range(620):
        x, y = rng.random() * GW, rng.random() * GH
        e = inside_e(x, y)
        if e < 1.15 and rng.random() < .9: continue
        if pmask[int(y * K) % NY, int(x * K) % NX] > .2: continue
        if rng.random() < .6:
            c = T['deco'][rng.integers(2)]
            for j in range(rng.integers(4, 9)):
                fx, fy = x + rng.normal() * 5, y + rng.normal() * 3; s2 = 1.2 + rng.random()
                d.ellipse([(fx - s2) * K, (fy - s2) * K, (fx + s2) * K, (fy + s2) * K], fill=c)
                d.ellipse([(fx - .5) * K, (fy - .5) * K, (fx + .5) * K, (fy + .5) * K], fill=(255, 240, 160))
        else:
            c = T['deco'][3]; w2, h2 = 3 + rng.random() * 5, 2 + rng.random() * 3
            d.ellipse([(x - w2) * K, (y - h2 + 1.5) * K, (x + w2) * K, (y + h2 + 1.5) * K], fill=tuple(int(v * .55) for v in c))
            d.ellipse([(x - w2) * K, (y - h2) * K, (x + w2) * K, (y + h2) * K], fill=c)
            d.ellipse([(x - w2 * .5) * K, (y - h2 * .8) * K, (x + w2 * .2) * K, (y - h2 * .2) * K], fill=tuple(min(255, int(v * 1.3)) for v in c))
    if T.get('cracks'):
        for i in range(160):
            x, y = rng.random() * GW, rng.random() * GH
            if ((x - CX) / WRX) ** 2 + ((y - CY) / WRY) ** 2 < 1.15: continue
            pts = [(x * K, y * K)]
            for j in range(6): x += rng.normal() * 10; y += rng.normal() * 8; pts.append((x * K, y * K))
            d.line(pts, fill=(255, 110, 30), width=3); d.line(pts, fill=(255, 210, 80), width=1)
    # surun dibi: hafif gölge halkası
    sh = mask_draw(lambda d2: ell(d2, CX, CY + 6, WRX + 6, WRY + 6, outline=255, width=16 * K), 8)
    arr = np.array(im).astype(float) * (1 - .3 * sh[..., None])
    # kenar karartma
    vr = np.sqrt(((xx - CX) / 420) ** 2 + ((yy - CY) / 620) ** 2)
    arr *= np.clip(1.12 - .22 * vr, .75, 1)[..., None]
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(out, quality=88)

for t in THEMES: build(t, f'{sys.argv[1]}/base_ground_{t}.jpg'); print(t)
