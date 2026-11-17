"""Yuvarlak üs için sur parçası ve kule görselleri üretir (kademe 0-5).
Çıktı: Assets/Resources/Art/wall_seg_t<k>.png (oyunda 26x34 px), wall_tower_t<k>.png (oyunda 40x60 px).
Ölçek: 4 doku pikseli = 1 oyun pikseli. Pivot alt orta (taban çizgisi en altta 2 oyun pikseli yukarıda).
Kullanım: python3 gen_wall.py <Assets/Resources/Art klasörü>"""
import sys
from PIL import Image, ImageDraw

S = 4
OUT = sys.argv[1] if len(sys.argv) > 1 else "."

# (ana renk, açık, koyu, süs rengi)  — kapı görselleriyle aynı palet
TIERS = [
    ((138, 100, 52), (170, 128, 70), (96, 68, 34), None),          # 0 ahşap çit
    ((150, 156, 166), (184, 190, 198), (104, 110, 122), None),     # 1 kaba taş
    ((140, 148, 160), (178, 186, 196), (92, 100, 114), (200, 60, 50)),   # 2 taş + bayrak
    ((104, 136, 186), (146, 176, 218), (70, 96, 140), (255, 168, 40)),   # 3 mavi taş + meşale
    ((88, 118, 170), (132, 162, 210), (56, 80, 124), (255, 168, 40)),    # 4 koyu mavi
    ((190, 168, 112), (226, 206, 150), (140, 120, 74), (255, 214, 60)),  # 5 altın
]


def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c)


def seg(t):
    base, light, dark, acc = TIERS[t]
    w, h = 26 * S, 34 * S
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # gölge
    d.ellipse([1 * S, h - 7 * S, w - 1 * S, h - 1 * S], fill=(0, 0, 0, 70))
    if t == 0:
        # sivri kazıklar
        for i in range(5):
            x0 = 1 * S + i * 5 * S
            top = 6 * S + (i % 2) * 2 * S
            col = base if i % 2 == 0 else shade(base, .88)
            d.rectangle([x0, top + 4 * S, x0 + 4 * S, h - 4 * S], fill=col, outline=dark, width=S // 2)
            d.polygon([(x0, top + 4 * S), (x0 + 2 * S, top), (x0 + 4 * S, top + 4 * S)], fill=light, outline=dark)
        d.rectangle([1 * S, 18 * S, w - 2 * S, 20 * S], fill=dark)
        d.rectangle([1 * S, 25 * S, w - 2 * S, 27 * S], fill=dark)
        return im
    top_y, face_y, bot = 8 * S, 15 * S, h - 4 * S
    # mazgallar
    for i in range(4):
        x0 = 1 * S + i * 7 * S
        d.rectangle([x0, 3 * S, x0 + 4 * S, top_y], fill=light, outline=dark, width=S // 2)
    # üst yüz
    d.rectangle([0, top_y, w - 1, face_y], fill=light, outline=dark, width=S // 2)
    # ön yüz
    d.rectangle([0, face_y, w - 1, bot], fill=base, outline=dark, width=S // 2)
    # taş derzleri
    rows = [face_y + 6 * S, face_y + 12 * S]
    for r, y in enumerate(rows):
        d.line([(0, y), (w, y)], fill=dark, width=S // 2)
    for r in range(3):
        y0 = face_y + r * 6 * S
        off = 0 if r % 2 == 0 else 4 * S
        for x in range(off + 8 * S, w, 9 * S):
            d.line([(x, y0), (x, min(bot, y0 + 6 * S))], fill=dark, width=S // 2)
    d.rectangle([0, bot - 2 * S, w - 1, bot], fill=shade(base, .75))
    if t >= 4:  # metal şerit
        d.rectangle([0, face_y + 1 * S, w - 1, face_y + 2 * S], fill=(70, 74, 84))
    if t == 5:
        d.rectangle([0, top_y, w - 1, top_y + 1 * S], fill=acc)
    return im


def tower(t):
    base, light, dark, acc = TIERS[t]
    w, h = 40 * S, 60 * S
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([2 * S, h - 9 * S, w - 2 * S, h - 1 * S], fill=(0, 0, 0, 80))
    x0, x1, bot = 6 * S, w - 6 * S, h - 4 * S
    if t == 0:
        # ahşap gözetleme platformu
        for x in (x0 + 2 * S, x1 - 4 * S):
            d.rectangle([x, 22 * S, x + 3 * S, bot], fill=dark)
        d.line([(x0 + 3 * S, 30 * S), (x1 - 3 * S, bot - 4 * S)], fill=dark, width=2 * S)
        d.line([(x1 - 3 * S, 30 * S), (x0 + 3 * S, bot - 4 * S)], fill=dark, width=2 * S)
        d.rectangle([x0 - 2 * S, 16 * S, x1 + 2 * S, 24 * S], fill=base, outline=dark, width=S)
        d.polygon([(x0 - 4 * S, 16 * S), (w // 2, 4 * S), (x1 + 4 * S, 16 * S)], fill=light, outline=dark)
        return im
    top = 14 * S
    # gövde
    d.rectangle([x0, top + 6 * S, x1, bot], fill=base, outline=dark, width=S // 2)
    d.rectangle([x1 - 8 * S, top + 6 * S, x1, bot], fill=shade(base, .82))
    for y in range(top + 12 * S, bot, 7 * S):
        d.line([(x0, y), (x1, y)], fill=dark, width=S // 2)
    # taç
    d.rectangle([x0 - 3 * S, top, x1 + 3 * S, top + 7 * S], fill=light, outline=dark, width=S // 2)
    for i in range(4):
        cx = x0 - 3 * S + i * 9 * S
        d.rectangle([cx, top - 5 * S, cx + 5 * S, top], fill=light, outline=dark, width=S // 2)
    # pencere
    d.rectangle([w // 2 - 3 * S, top + 14 * S, w // 2 + 2 * S, top + 22 * S], fill=(30, 34, 44))
    if acc and t >= 2:
        # bayrak
        d.line([(w // 2, top - 5 * S), (w // 2, 0)], fill=(70, 50, 30), width=S)
        d.polygon([(w // 2 + S, 0), (w // 2 + 11 * S, 3 * S), (w // 2 + S, 6 * S)], fill=acc if t == 5 else (200, 60, 50))
    if acc and t in (3, 4):
        # meşale
        d.ellipse([x0 + 2 * S, top + 26 * S, x0 + 6 * S, top + 31 * S], fill=acc)
    if t == 5:
        d.rectangle([x0 - 3 * S, top + 6 * S, x1 + 3 * S, top + 7 * S], fill=acc)
    return im


for t in range(6):
    seg(t).save(f"{OUT}/wall_seg_t{t}.png")
    tower(t).save(f"{OUT}/wall_tower_t{t}.png")
print("ok")
