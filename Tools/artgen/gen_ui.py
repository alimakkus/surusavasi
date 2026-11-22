"""Arayüz görselleri: düğmeler (4 renk × normal/üstünde/basılı + pasif), kartlar, şerit başlık, kaynak simgeleri.
4 kat büyük çizilip küçültülür (kenar yumuşatma). Çıktı: Assets/Resources/UI/
Kullanım: python3 gen_ui.py <Assets/Resources/UI klasörü>"""
import sys, os, math
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "UI"
os.makedirs(OUT, exist_ok=True)
SS = 4


def hexc(h, a=255):
    h = h.lstrip("#"); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def rr_mask(w, h, r, box=None):
    m = Image.new("L", (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle(box or [0, 0, w - 1, h - 1], radius=r, fill=255)
    return m


def vgrad(w, h, top, bot):
    g = Image.new("RGBA", (w, h))
    px = g.load()
    for y in range(h):
        c = mix(top, bot, y / max(1, h - 1))
        for x in range(w):
            px[x, y] = c
    return g


def button(name, base, light, dark, outline, depth=5, pressed=False, gloss=.28):
    W = H = 48 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    r = 12 * SS
    d = (2 if pressed else depth) * SS
    top_off = (depth - 2) * SS if pressed else 0
    # dış çizgi + derinlik (alt kısım koyu)
    outer = rr_mask(W, H, r, [0, top_off, W - 1, H - 1])
    img.paste(Image.new("RGBA", (W, H), outline), (0, 0), outer)
    depthm = rr_mask(W, H, r - SS, [2 * SS, top_off + 2 * SS, W - 1 - 2 * SS, H - 1 - 2 * SS])
    img.paste(Image.new("RGBA", (W, H), dark), (0, 0), depthm)
    # gövde degrade
    bx0, by0, bx1, by1 = 2 * SS, top_off + 2 * SS, W - 1 - 2 * SS, H - 1 - 2 * SS - d
    bodym = rr_mask(W, H, r - SS, [bx0, by0, bx1, by1])
    img.paste(vgrad(W, H, light, base).crop((0, 0, W, H)), (0, 0), bodym)
    # parlaklık bandı (üst yarı)
    gl = Image.new("L", (W, H), 0)
    ImageDraw.Draw(gl).rounded_rectangle([bx0 + 3 * SS, by0 + 2 * SS, bx1 - 3 * SS, by0 + (by1 - by0) * .48], radius=r - 4 * SS, fill=int(255 * gloss))
    gl = gl.filter(ImageFilter.GaussianBlur(SS * 1.2))
    img.paste(Image.new("RGBA", (W, H), (255, 255, 255, 255)), (0, 0), Image.composite(gl, Image.new("L", (W, H), 0), bodym))
    # iç üst ince ışık çizgisi
    ln = Image.new("L", (W, H), 0)
    ImageDraw.Draw(ln).rounded_rectangle([bx0, by0, bx1, by1], radius=r - SS, outline=110, width=SS)
    cut = Image.new("L", (W, H), 0); ImageDraw.Draw(cut).rectangle([0, 0, W, by0 + (by1 - by0) * .5], fill=255)
    img.paste(Image.new("RGBA", (W, H), (255, 255, 255, 255)), (0, 0), Image.composite(ln, Image.new("L", (W, H), 0), cut))
    img.resize((48, 48), Image.LANCZOS).save(f"{OUT}/{name}.png")


PAL = {
    # ad: (gövde, açık, derinlik, dış çizgi)
    "blue": ("#2a6fc4", "#5aa6f0", "#174178", "#0c2242"),
    "gold": ("#e09a12", "#ffd85a", "#8a5806", "#4a2d02"),
    "green": ("#2f9e44", "#6fdc72", "#1b5e2a", "#0b2e14"),
    "red": ("#c8362e", "#ff7a62", "#7a1814", "#3a0806"),
    "gray": ("#5c6470", "#8d96a3", "#363c45", "#1a1d22"),
}
for k, (b, l, d, o) in PAL.items():
    B, L, D, O = hexc(b), hexc(l), hexc(d), hexc(o)
    button(f"btn_{k}", B, L, D, O)
    button(f"btn_{k}_hover", mix(B, (255, 255, 255, 255), .12), mix(L, (255, 255, 255, 255), .15), D, O)
    button(f"btn_{k}_press", mix(B, (0, 0, 0, 255), .12), B, D, O, pressed=True)


def card(name, body_top, body_bot, rim, rim_light, inner, rivets=True, s=64):
    W = H = s * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    r = 14 * SS
    img.paste(Image.new("RGBA", (W, H), hexc("#05070b", 230)), (0, 0), rr_mask(W, H, r))
    img.paste(vgrad(W, H, hexc(rim_light), hexc(rim)), (0, 0), rr_mask(W, H, r - SS, [SS, SS, W - 1 - SS, H - 1 - SS]))
    img.paste(Image.new("RGBA", (W, H), hexc("#0a0d14", 255)), (0, 0), rr_mask(W, H, r - 3 * SS, [4 * SS, 4 * SS, W - 1 - 4 * SS, H - 1 - 4 * SS]))
    img.paste(vgrad(W, H, body_top, body_bot), (0, 0), rr_mask(W, H, r - 4 * SS, [5 * SS, 5 * SS, W - 1 - 5 * SS, H - 1 - 5 * SS]))
    ln = Image.new("L", (W, H), 0)
    ImageDraw.Draw(ln).rounded_rectangle([6 * SS, 6 * SS, W - 1 - 6 * SS, H - 1 - 6 * SS], radius=r - 5 * SS, outline=60, width=SS)
    img.paste(Image.new("RGBA", (W, H), hexc(inner)), (0, 0), ln)
    if rivets:
        dr = ImageDraw.Draw(img)
        for cx, cy in [(9, 9), (s - 10, 9), (9, s - 10), (s - 10, s - 10)]:
            dr.ellipse([(cx - 2.2) * SS, (cy - 2.2) * SS, (cx + 2.2) * SS, (cy + 2.2) * SS], fill=hexc(rim_light), outline=hexc("#2a1c08"), width=SS // 2)
    img.resize((s, s), Image.LANCZOS).save(f"{OUT}/{name}.png")


card("card", hexc("#1c2536", 245), hexc("#111724", 245), "#7a5a26", "#e7c26a", "#ffffff")
card("card_gold", hexc("#3a2c12", 245), hexc("#1d160a", 245), "#a8741a", "#ffe08a", "#fff3c0")
card("card_dark", hexc("#141a26", 235), hexc("#0c111a", 235), "#3b475c", "#7c8aa3", "#ffffff", rivets=False)


def ribbon():
    W, H = 96 * SS, 40 * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    # uçlar
    for side in (0, 1):
        x0 = 0 if side == 0 else W - 18 * SS
        pts = [(x0, 8 * SS), (x0 + 18 * SS, 8 * SS), (x0 + 18 * SS, 36 * SS), (x0, 36 * SS), (x0 + 7 * SS, 22 * SS)] if side == 0 else \
              [(x0 + 18 * SS, 8 * SS), (x0, 8 * SS), (x0, 36 * SS), (x0 + 18 * SS, 36 * SS), (x0 + 11 * SS, 22 * SS)]
        dr.polygon(pts, fill=hexc("#8a1f19"), outline=hexc("#3a0806"))
    body = rr_mask(W, H, 6 * SS, [10 * SS, 2 * SS, W - 10 * SS, 32 * SS])
    img.paste(Image.new("RGBA", (W, H), hexc("#3a0806")), (0, 0), body)
    img.paste(vgrad(W, H, hexc("#ff6a52"), hexc("#b3261e")), (0, 0), rr_mask(W, H, 5 * SS, [12 * SS, 4 * SS, W - 12 * SS, 30 * SS]))
    gl = Image.new("L", (W, H), 0); ImageDraw.Draw(gl).rectangle([14 * SS, 5 * SS, W - 14 * SS, 14 * SS], fill=60)
    img.paste(Image.new("RGBA", (W, H), (255, 255, 255, 255)), (0, 0), gl)
    img.resize((96, 40), Image.LANCZOS).save(f"{OUT}/ribbon.png")


ribbon()


# ---------- Simgeler (64x64) ----------
def icon(name, draw_fn, s=64):
    W = s * SS
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    draw_fn(img, ImageDraw.Draw(img), W)
    # hafif gölge
    sh = img.split()[3].filter(ImageFilter.GaussianBlur(SS * 2))
    shadow = Image.new("RGBA", (W, W), (0, 0, 0, 0)); shadow.paste(Image.new("RGBA", (W, W), (0, 0, 0, 120)), (SS * 2, SS * 3), sh)
    out = Image.alpha_composite(shadow, img)
    out.resize((s, s), Image.LANCZOS).save(f"{OUT}/{name}.png")


def coin(img, d, W):
    c = W / 2; R = W * .40
    d.ellipse([c - R, c - R + W * .04, c + R, c + R + W * .04], fill=hexc("#8a5806"))
    d.ellipse([c - R, c - R, c + R, c + R], fill=hexc("#f2b31e"), outline=hexc("#6b4204"), width=SS * 2)
    r2 = R * .74
    d.ellipse([c - r2, c - r2, c + r2, c + r2], fill=hexc("#ffd24a"), outline=hexc("#c8860c"), width=SS * 2)
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5; rr = r2 * (.62 if i % 2 == 0 else .27)
        pts.append((c + rr * math.cos(a), c + rr * math.sin(a)))
    d.polygon(pts, fill=hexc("#f2a910"), outline=hexc("#b97806"))
    d.ellipse([c - R * .62, c - R * .78, c - R * .1, c - R * .45], fill=(255, 255, 255, 120))


def ingot(img, d, W):
    t = [(W * .26, W * .30), (W * .74, W * .30), (W * .90, W * .56), (W * .10, W * .56)]
    d.polygon(t, fill=hexc("#c9d2dc"), outline=hexc("#3c4652"))
    f = [(W * .10, W * .56), (W * .90, W * .56), (W * .90, W * .72), (W * .10, W * .72)]
    d.polygon(f, fill=hexc("#7d8896"), outline=hexc("#3c4652"))
    d.polygon([(W * .74, W * .30), (W * .90, W * .56), (W * .90, W * .72), (W * .74, W * .46)], fill=hexc("#5c6674"), outline=hexc("#3c4652"))
    d.line([(W * .30, W * .36), (W * .62, W * .36)], fill=(255, 255, 255, 200), width=SS * 3)


def food(img, d, W):
    # et budu
    d.ellipse([W * .18, W * .14, W * .70, W * .62], fill=hexc("#b5502a"), outline=hexc("#4a1c08"), width=SS * 2)
    d.ellipse([W * .24, W * .18, W * .62, W * .50], fill=hexc("#d8743e"))
    d.ellipse([W * .28, W * .22, W * .44, W * .32], fill=(255, 255, 255, 110))
    d.line([(W * .56, W * .52), (W * .78, W * .76)], fill=hexc("#f2ead8"), width=SS * 7)
    for ox, oy in [(.74, .76), (.80, .70)]:
        d.ellipse([W * (ox - .06), W * (oy - .06), W * (ox + .06), W * (oy + .06)], fill=hexc("#f2ead8"), outline=hexc("#8a7a5a"), width=SS)


def gem(img, d, W):
    c = W / 2
    top = [(W * .24, W * .36), (W * .36, W * .20), (W * .64, W * .20), (W * .76, W * .36)]
    d.polygon(top + [(c, W * .84)][:0] + [(W * .76, W * .36)], fill=hexc("#7fe6ff"))
    d.polygon([(W * .24, W * .36), (W * .76, W * .36), (c, W * .84)], fill=hexc("#2aa9e0"), outline=hexc("#0b3e6e"))
    d.polygon(top, fill=hexc("#a8f0ff"), outline=hexc("#0b3e6e"))
    d.polygon([(W * .36, W * .36), (c, W * .84), (W * .24, W * .36)], fill=hexc("#58c8f0"))
    d.polygon([(W * .40, W * .22), (W * .50, W * .22), (W * .44, W * .34)], fill=(255, 255, 255, 200))


def star(img, d, W):
    c = W / 2; pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5; rr = W * (.42 if i % 2 == 0 else .18)
        pts.append((c + rr * math.cos(a), c + W * .04 + rr * math.sin(a)))
    d.polygon(pts, fill=hexc("#ffcf2e"), outline=hexc("#8a5806"))
    inner = [(c + (x - c) * .6, c + W * .02 + (y - c - W * .04) * .6) for x, y in pts]
    d.polygon(inner, fill=hexc("#ffe680"))


def shard(img, d, W):
    d.polygon([(W * .5, W * .12), (W * .74, W * .46), (W * .54, W * .88), (W * .30, W * .52)], fill=hexc("#b066ff"), outline=hexc("#3c1470"))
    d.polygon([(W * .5, W * .12), (W * .30, W * .52), (W * .48, W * .50)], fill=hexc("#d7a6ff"))


for n, f in [("ic_gold", coin), ("ic_iron", ingot), ("ic_food", food), ("ic_gem", gem), ("ic_star", star), ("ic_shard", shard)]:
    icon(n, f)
print("ok")


# ---------- Altıgen menü düğmeleri (96x96) ----------
def hexbtn(name, base, light, dark, outline):
    S = 96 * SS; c = S / 2
    def hexpts(R, cy):
        return [(c + R * math.cos(math.radians(60 * i)), cy + R * math.sin(math.radians(60 * i))) for i in range(6)]
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    R = S * .47
    d.polygon(hexpts(R, c + 3 * SS), fill=hexc(outline))
    d.polygon(hexpts(R - 3 * SS, c + 3 * SS), fill=hexc(dark))
    body = Image.new("L", (S, S), 0); ImageDraw.Draw(body).polygon(hexpts(R - 3 * SS, c - 2 * SS), fill=255)
    img.paste(vgrad(S, S, hexc(light), hexc(base)), (0, 0), body)
    gl = Image.new("L", (S, S), 0); ImageDraw.Draw(gl).ellipse([c - R * .62, c - R * .82, c + R * .62, c - R * .05], fill=70)
    gl = gl.filter(ImageFilter.GaussianBlur(SS * 2))
    img.paste(Image.new("RGBA", (S, S), (255, 255, 255, 255)), (0, 0), Image.composite(gl, Image.new("L", (S, S), 0), body))
    ln = Image.new("L", (S, S), 0); ImageDraw.Draw(ln).polygon(hexpts(R - 5 * SS, c - 2 * SS), outline=120)
    img.paste(Image.new("RGBA", (S, S), (255, 255, 255, 255)), (0, 0), ln)
    img.resize((96, 96), Image.LANCZOS).save(f"{OUT}/{name}.png")


hexbtn("hex_blue", "#2a6fc4", "#5aa6f0", "#174178", "#0c2242")
hexbtn("hex_green", "#2f9e44", "#6fdc72", "#1b5e2a", "#0b2e14")
hexbtn("hex_orange", "#e07a12", "#ffc45a", "#8a4806", "#4a2402")
print("hex ok")
