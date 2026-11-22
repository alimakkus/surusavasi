"""Yuvarlak üs için sur parçası ve kule görselleri (kademe 0-5), dokulu taş ve gölgeli.
Çıktı: Assets/Resources/Art/wall_seg_t<k>.png (oyunda 30x40 px), wall_tower_t<k>.png (oyunda 46x76 px).
Ölçek: 4 doku pikseli = 1 oyun pikseli; 3 kat büyük çizilip küçültülür. Pivot alt orta (alttan 2 oyun pikseli).
Kullanım: python3 gen_wall.py <Assets/Resources/Art klasörü>"""
import sys, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
K = 4 * 3          # 1 oyun pikseli = 12 çizim pikseli (sonra 3 kat küçülür)

# kademe: (taş açık, taş koyu, harç, çatı, çatı koyu, süs)
TIERS = [
    dict(kind="wood", stone=(150, 108, 60), dark=(92, 62, 32), mortar=(70, 46, 22), roof=(118, 84, 46), roofd=(80, 54, 28), acc=None),
    dict(kind="stone", stone=(150, 146, 132), dark=(98, 94, 84), mortar=(66, 62, 56), roof=None, roofd=None, acc=None),
    dict(kind="stone", stone=(168, 170, 176), dark=(104, 108, 118), mortar=(70, 72, 80), roof=(176, 58, 46), roofd=(112, 32, 26), acc=(200, 50, 40)),
    dict(kind="stone", stone=(150, 166, 196), dark=(86, 100, 136), mortar=(56, 66, 92), roof=(52, 98, 186), roofd=(28, 56, 116), acc=(255, 168, 40)),
    dict(kind="stone", stone=(112, 118, 132), dark=(62, 66, 78), mortar=(36, 38, 46), roof=(70, 74, 88), roofd=(34, 36, 46), acc=(255, 150, 40)),
    dict(kind="stone", stone=(222, 198, 140), dark=(156, 128, 78), mortar=(110, 88, 50), roof=(232, 176, 44), roofd=(150, 104, 18), acc=(255, 214, 60)),
]


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def noise(w, h, seed, scale=6):
    rng = np.random.default_rng(seed)
    small = rng.random((max(2, h // scale), max(2, w // scale)))
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def texture(img, mask, seed, amt=.18):
    """Taş yüzeyine hafif lekeli doku uygular (yalnızca mask içinde)."""
    a = np.asarray(img, dtype=np.float32)
    n = noise(img.width, img.height, seed, 9) * .6 + noise(img.width, img.height, seed + 7, 3) * .4
    f = 1 + (n - .5) * 2 * amt
    m = np.asarray(mask, dtype=np.float32)[..., None] / 255.0
    a[..., :3] = a[..., :3] * (1 - m) + np.clip(a[..., :3] * f[..., None], 0, 255) * m
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def bricks(d, x0, y0, x1, y1, T, rnd, bh):
    """Tuğla sıraları: her taş biraz farklı tonda, harç aralıklı, üst kenarı açık."""
    row = 0; y = y0
    while y < y1:
        h = min(bh, y1 - y); off = (row % 2) * bh * .9
        x = x0 - off
        while x < x1:
            w = bh * (1.4 + rnd.random() * .8)
            xa, xb = max(x0, x), min(x1, x + w)
            if xb - xa > K:
                f = .86 + rnd.random() * .24
                d.rectangle([xa + K * .4, y + K * .4, xb - K * .4, y + h - K * .4], fill=shade(T["stone"], f))
                d.line([(xa + K * .6, y + K * .6), (xb - K * .6, y + K * .6)], fill=shade(T["stone"], f * 1.16), width=int(K * .5))
                d.line([(xa + K * .6, y + h - K * .7), (xb - K * .6, y + h - K * .7)], fill=shade(T["stone"], f * .72), width=int(K * .5))
            x += w
        y += h; row += 1


def seg(t):
    T = TIERS[t]; rnd = random.Random(t * 31 + 5)
    w, h = 30 * K, 40 * K
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.ellipse([0, h - 8 * K, w, h - 0.5 * K], fill=(0, 0, 0, 80))
    bot = h - 3 * K
    if T["kind"] == "wood":
        for i in range(6):
            x0 = i * 5 * K; top = (5 + (i % 2) * 2) * K
            col = shade(T["stone"], .9 + rnd.random() * .2)
            d.rectangle([x0 + K * .3, top + 4 * K, x0 + 4.7 * K, bot], fill=col, outline=T["dark"], width=int(K * .6))
            d.polygon([(x0 + K * .3, top + 4 * K), (x0 + 2.5 * K, top), (x0 + 4.7 * K, top + 4 * K)], fill=shade(col, 1.15), outline=T["dark"])
            for yy in range(int(top / K) + 7, 36, 4):
                d.line([(x0 + K, yy * K), (x0 + 4 * K, yy * K + K)], fill=shade(col, .78), width=int(K * .4))
        for yy in (17, 29):
            d.rectangle([0, yy * K, w, (yy + 2) * K], fill=T["mortar"])
            d.line([(0, yy * K), (w, yy * K)], fill=shade(T["mortar"], 1.4), width=int(K * .5))
        return img
    face_y, top_y = 15 * K, 9 * K
    # mazgallar (arka sıra koyu, ön sıra açık)
    for i in range(4):
        x0 = (1 + i * 7.5) * K
        d.rectangle([x0, 3 * K, x0 + 5 * K, top_y + K], fill=shade(T["stone"], 1.05), outline=T["mortar"], width=int(K * .6))
        d.rectangle([x0, 3 * K, x0 + 5 * K, 4.4 * K], fill=shade(T["stone"], 1.25))
        d.rectangle([x0 + 3.6 * K, 4 * K, x0 + 5 * K, top_y + K], fill=shade(T["stone"], .8))
    # yürüyüş yolu (üst yüz)
    d.rectangle([0, top_y, w - 1, face_y], fill=shade(T["stone"], 1.12), outline=T["mortar"], width=int(K * .6))
    d.rectangle([0, face_y - 1.5 * K, w - 1, face_y], fill=shade(T["stone"], .75))
    # ön yüz: harç zemin + tuğlalar
    d.rectangle([0, face_y, w - 1, bot], fill=T["mortar"])
    bricks(d, 0, face_y, w, bot, T, rnd, 5.6 * K)
    # taban gölgesi ve yosun / kum
    d.rectangle([0, bot - 2.2 * K, w - 1, bot], fill=shade(T["dark"], .85))
    moss = (86, 120, 56) if t < 5 else (196, 170, 110)
    for _ in range(9):
        mx = rnd.random() * w; my = bot - rnd.random() * 4 * K; r = (1 + rnd.random() * 1.6) * K
        d.ellipse([mx - r, my - r * .6, mx + r, my + r * .6], fill=moss + (170,))
    if t >= 4:  # demir şerit ve perçin
        d.rectangle([0, face_y + 1.2 * K, w - 1, face_y + 2.6 * K], fill=(62, 66, 78))
        for x in range(2, 30, 6):
            d.ellipse([(x - .7) * K, face_y + 1.2 * K, (x + .7) * K, face_y + 2.6 * K], fill=(170, 176, 190))
    if t == 5:
        d.rectangle([0, top_y, w - 1, top_y + 1.2 * K], fill=T["acc"])
    mask = img.split()[3]
    return texture(img, mask, t * 13 + 1)


def tower(t):
    T = TIERS[t]; rnd = random.Random(t * 17 + 3)
    w, h = 46 * K, 76 * K
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    d.ellipse([2 * K, h - 11 * K, w - 2 * K, h - 1 * K], fill=(0, 0, 0, 90))
    bot = h - 4 * K; cx = w / 2
    if T["kind"] == "wood":
        for x in (8 * K, w - 12 * K):
            d.rectangle([x, 30 * K, x + 4 * K, bot], fill=T["dark"])
        d.line([(10 * K, 38 * K), (w - 10 * K, bot - 6 * K)], fill=T["dark"], width=int(2 * K))
        d.line([(w - 10 * K, 38 * K), (10 * K, bot - 6 * K)], fill=T["dark"], width=int(2 * K))
        d.rectangle([5 * K, 22 * K, w - 5 * K, 32 * K], fill=T["stone"], outline=T["dark"], width=K)
        for x in range(6, 41, 4):
            d.line([(x * K, 22 * K), (x * K, 32 * K)], fill=shade(T["stone"], .8), width=int(K * .5))
        d.polygon([(2 * K, 22 * K), (cx, 6 * K), (w - 2 * K, 22 * K)], fill=T["roof"], outline=T["roofd"])
        d.line([(cx, 6 * K), (cx, 0)], fill=T["dark"], width=K)
        return img
    x0, x1 = 8 * K, w - 8 * K
    body_top = 30 * K if T["roof"] else 20 * K
    # gövde (silindir: kenarlar koyu)
    d.rectangle([x0, body_top, x1, bot], fill=T["mortar"])
    bricks(d, x0, body_top, x1, bot, T, rnd, 5 * K)
    a = np.asarray(img, dtype=np.float32)
    xs = np.arange(w)
    cyl = 1 - .45 * ((xs - cx) / ((x1 - x0) / 2)) ** 2
    cyl = np.clip(cyl, .55, 1)
    m = np.zeros((h, w), dtype=bool); m[int(body_top):int(bot), int(x0):int(x1)] = True
    a[m, :3] *= np.broadcast_to(cyl[None, :, None], (h, w, 1))[m]
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA"); d = ImageDraw.Draw(img)
    # pencere
    d.rounded_rectangle([cx - 3 * K, body_top + 12 * K, cx + 3 * K, body_top + 22 * K], radius=3 * K, fill=(26, 22, 30), outline=T["dark"], width=K)
    # taç (mazgallı)
    cy0 = body_top - 6 * K
    d.rectangle([x0 - 3 * K, cy0, x1 + 3 * K, body_top + 1 * K], fill=shade(T["stone"], 1.08), outline=T["mortar"], width=int(K * .6))
    d.rectangle([x0 - 3 * K, body_top - 1 * K, x1 + 3 * K, body_top + 1 * K], fill=shade(T["dark"], .9))
    if T["roof"] is None:
        for i in range(5):
            mx = x0 - 3 * K + i * ((x1 - x0 + 6 * K - 5 * K) / 4)
            d.rectangle([mx, cy0 - 5 * K, mx + 5 * K, cy0], fill=shade(T["stone"], 1.1), outline=T["mortar"], width=int(K * .6))
    else:
        # konik çatı, kiremit sıraları
        apex = 3 * K
        d.polygon([(x0 - 6 * K, cy0 + K), (cx, apex), (x1 + 6 * K, cy0 + K)], fill=T["roof"], outline=T["roofd"])
        for k in range(1, 6):
            yy = apex + (cy0 - apex) * k / 6
            half = (x1 - x0 + 12 * K) / 2 * k / 6
            d.line([(cx - half, yy), (cx + half, yy)], fill=T["roofd"], width=int(K * .7))
        d.polygon([(cx, apex), (cx + (x1 - x0) / 2 + 6 * K, cy0 + K), (cx + 2 * K, cy0 + K)], fill=shade(T["roof"], .78))
        d.line([(cx, apex), (cx, 0)], fill=(70, 50, 30), width=int(K * .9))
        if T["acc"]:
            d.polygon([(cx + K, 0), (cx + 12 * K, 3 * K), (cx + K, 6 * K)], fill=T["acc"])
    if t in (3, 4):  # meşaleler
        for sx in (x0 + 1 * K, x1 - 3 * K):
            d.rectangle([sx, body_top + 26 * K, sx + 2 * K, body_top + 30 * K], fill=(70, 50, 30))
            d.ellipse([sx - K, body_top + 22 * K, sx + 3 * K, body_top + 27 * K], fill=T["acc"])
    mask = img.split()[3]
    return texture(img, mask, t * 7 + 2, .14)


for t in range(6):
    seg(t).resize((30 * 4, 40 * 4), Image.LANCZOS).save(f"{OUT}/wall_seg_t{t}.png")
    tower(t).resize((46 * 4, 76 * 4), Image.LANCZOS).save(f"{OUT}/wall_tower_t{t}.png")
print("ok")
