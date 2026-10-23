# Potato Tweaks page icons (Valheim): small pixel icons drawn on a 20x20 grid, outlined and scaled 4x to 80 px,
# to match PotatoFarmer's 80 px piece icons. Run: python3 -I tools/icons.py docs/images
import sys, os, math
from PIL import Image, ImageDraw

N, S = 20, 4
OUT = (38, 24, 14, 255)  # outline

def canvas():
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)

def shade(c, f):
    return tuple(max(0, min(255, int(v * f))) for v in c[:3]) + (255,)

def finish(im, name, out):
    # Dark outline around everything drawn, then scale up.
    px = im.load()
    o = im.copy(); po = o.load()
    for y in range(N):
        for x in range(N):
            if px[x, y][3] == 0 and any(0 <= x + dx < N and 0 <= y + dy < N and px[x + dx, y + dy][3] > 0
                                         for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                po[x, y] = OUT
    o.resize((N * S, N * S), Image.NEAREST).save(os.path.join(out, f"icon-{name}.png"))
    return o

WOOD, WOOD_D, WOOD_L = (150, 95, 50), (105, 62, 32), (190, 130, 72)
IRON, IRON_D, IRON_L = (120, 120, 125), (80, 80, 88), (175, 175, 180)
GOLD, GOLD_D, GOLD_L = (222, 170, 60), (160, 110, 30), (250, 220, 120)

def chest():
    im, d = canvas()
    d.rectangle((2, 8, 17, 17), fill=WOOD)
    d.rectangle((2, 4, 17, 8), fill=WOOD_L)          # lid
    d.line((2, 4, 17, 4), fill=shade(WOOD_L, 1.1))
    d.line((2, 17, 17, 17), fill=WOOD_D)
    d.line((2, 9, 17, 9), fill=WOOD_D)               # lid seam
    for x in (4, 15):                                # iron bands
        d.line((x, 4, x, 17), fill=IRON_D)
    d.rectangle((8, 8, 11, 11), fill=IRON)           # lock
    d.point((9, 10), fill=OUT); d.point((10, 10), fill=OUT)
    return im

def map_():
    im, d = canvas()
    P, PD = (225, 205, 160), (185, 160, 115)
    d.rectangle((3, 4, 16, 15), fill=P)
    d.rectangle((1, 3, 3, 16), fill=PD); d.rectangle((16, 3, 18, 16), fill=PD)   # rolls
    d.line((2, 3, 2, 16), fill=shade(PD, 1.15)); d.line((17, 3, 17, 16), fill=shade(PD, 1.15))
    for x, y in ((5, 12), (7, 11), (8, 9), (10, 8)):                            # dotted path
        d.point((x, y), fill=(120, 90, 60))
    R = (190, 40, 30)
    d.line((11, 5, 14, 8), fill=R); d.line((14, 5, 11, 8), fill=R)              # the X
    return im

def house():
    im, d = canvas()
    d.polygon([(1, 10), (10, 2), (18, 10)], fill=(110, 80, 45))                  # thatch roof
    d.line((1, 10, 10, 2), fill=(150, 112, 62)); d.line((10, 2, 18, 10), fill=(85, 60, 32))
    d.rectangle((3, 10, 16, 17), fill=WOOD)
    for y in (12, 14, 16):
        d.line((3, y, 16, y), fill=WOOD_D)
    d.rectangle((8, 12, 11, 17), fill=(60, 38, 22))                               # door
    d.point((10, 15), fill=GOLD)
    d.point((9, 1), fill=WOOD_L); d.point((11, 1), fill=WOOD_L)                 # crossed gable ends
    return im

def anvil():
    im, d = canvas()
    d.polygon([(2, 6), (17, 6), (15, 9), (4, 9)], fill=IRON)                     # face
    d.line((2, 6, 17, 6), fill=IRON_L)
    d.polygon([(0, 6), (3, 6), (4, 8)], fill=IRON)                                # horn
    d.rectangle((7, 9, 12, 13), fill=IRON_D)                                      # waist
    d.rectangle((4, 13, 15, 16), fill=IRON)
    d.line((4, 16, 15, 16), fill=IRON_D)
    d.rectangle((3, 17, 16, 18), fill=WOOD_D)                                     # stump
    for x, y in ((10, 3), (13, 2), (8, 2)):                                        # sparks
        d.point((x, y), fill=(255, 190, 60))
    return im

def shield():
    im, d = canvas()
    c, r = 9.5, 8.6
    for y in range(N):
        for x in range(N):
            dd = math.hypot(x - c, y - c)
            if dd <= r:
                plank = (x // 4) % 2
                col = WOOD if plank else (165, 52, 40)                            # painted planks
                if dd > r - 1.4: col = IRON_D if (x + y) % 2 else IRON           # iron rim
                im.putpixel((x, y), shade(col, 1.12 if x + y < 17 else 0.9))
    d.ellipse((7, 7, 12, 12), fill=IRON)                                          # boss
    d.point((8, 8), fill=IRON_L); d.point((9, 8), fill=IRON_L)
    return im

def pouch():
    im, d = canvas()
    L, LD, LL = (140, 98, 58), (100, 68, 38), (178, 130, 82)
    d.ellipse((3, 7, 16, 18), fill=L)
    d.polygon([(6, 8), (13, 8), (12, 4), (7, 4)], fill=L)                         # neck
    d.line((6, 8, 13, 8), fill=(200, 170, 100))                                   # drawstring
    d.line((5, 11, 6, 14), fill=LL)
    d.arc((3, 7, 16, 18), 20, 160, fill=LD)
    d.point((14, 7), fill=(200, 170, 100)); d.point((15, 6), fill=(200, 170, 100))
    d.ellipse((7, 2, 12, 5), fill=GOLD); d.point((8, 3), fill=GOLD_L)             # coin peeking out
    return im

def fire():
    im, d = canvas()
    d.line((3, 17, 16, 14), fill=WOOD_D, width=2); d.line((3, 14, 16, 17), fill=WOOD, width=2)   # logs
    d.polygon([(5, 14), (7, 7), (9, 10), (10, 3), (12, 9), (13, 7), (15, 14)], fill=(230, 110, 30))
    d.polygon([(7, 14), (9, 9), (10, 11), (11, 7), (13, 14)], fill=(250, 190, 50))
    d.polygon([(9, 14), (10, 11), (11, 14)], fill=(255, 240, 160))
    return im

def eye():
    im, d = canvas()
    d.polygon([(1, 10), (5, 6), (10, 5), (15, 6), (19, 10), (15, 14), (10, 15), (5, 14)], fill=(240, 235, 220))
    d.ellipse((6, 6, 13, 13), fill=(70, 130, 170))
    d.ellipse((8, 8, 11, 11), fill=(20, 25, 35))
    d.point((8, 8), fill=(255, 255, 255))
    for x, y in ((3, 3), (16, 2), (17, 17)):                                       # sparkles
        d.point((x, y), fill=(255, 230, 120))
    return im

def hammer():
    im, d = canvas()
    d.rectangle((9, 7, 10, 18), fill=WOOD)                                        # handle
    d.line((10, 7, 10, 18), fill=WOOD_D)
    d.rectangle((9, 15, 10, 17), fill=(90, 60, 40))                               # leather grip
    d.rectangle((3, 2, 16, 7), fill=IRON)                                         # head
    d.line((3, 2, 16, 2), fill=IRON_L); d.line((3, 7, 16, 7), fill=IRON_D)
    d.line((3, 2, 3, 7), fill=IRON_L); d.line((16, 2, 16, 7), fill=IRON_D)
    d.rectangle((8, 3, 11, 6), fill=IRON_D)                                       # socket band
    return im

def gem():
    im, d = canvas()
    P, PD, PL = (150, 70, 210), (95, 40, 150), (205, 150, 250)
    d.polygon([(4, 7), (7, 3), (12, 3), (15, 7), (9.5, 17)], fill=P)
    d.polygon([(4, 7), (15, 7), (9.5, 17)], fill=PD)
    d.polygon([(7, 7), (12, 7), (9.5, 17)], fill=P)
    d.line((7, 3, 12, 3), fill=PL); d.point((7, 5), fill=(240, 220, 255))
    for x, y in ((2, 2), (17, 4), (16, 15)):
        d.point((x, y), fill=(230, 200, 255))
    return im

def coins():
    im, d = canvas()
    for y in (15, 12, 9, 6):                                                      # a neat stack
        d.ellipse((3, y, 16, y + 4), fill=GOLD_D)
        d.ellipse((3, y - 1, 16, y + 3), fill=GOLD)
        d.line((6, y, 13, y), fill=GOLD_L)
    d.ellipse((6, 4, 13, 7), outline=GOLD_D)
    return im

def door():
    im, d = canvas()
    d.rectangle((4, 2, 15, 18), fill=WOOD)                                        # planks
    for x in (7, 10, 13):
        d.line((x, 2, x, 18), fill=WOOD_D)
    d.line((4, 2, 15, 2), fill=WOOD_L)
    for y in (5, 14):                                                             # iron hinges
        d.line((4, y, 12, y), fill=IRON_D); d.point((4, y), fill=IRON_L)
    d.rectangle((13, 9, 14, 11), fill=IRON)                                       # ring handle
    d.point((14, 12), fill=IRON_L)
    return im

def smelter():
    im, d = canvas()
    S, SD, SL = (95, 92, 98), (62, 60, 66), (135, 132, 138)
    d.polygon([(5, 18), (3, 9), (6, 3), (13, 3), (16, 9), (14, 18)], fill=S)       # stone body
    d.line((3, 9, 6, 3), fill=SL); d.line((6, 3, 13, 3), fill=SL); d.line((14, 18, 16, 9), fill=SD)
    for x, y in ((6, 7), (12, 6), (5, 14), (13, 13)):
        d.point((x, y), fill=SD)
    d.rectangle((7, 10, 12, 14), fill=(240, 120, 30))                             # fire mouth
    d.rectangle((8, 11, 11, 13), fill=(255, 200, 70))
    d.point((9, 12), fill=(255, 245, 180))
    d.rectangle((8, 0, 11, 2), fill=(255, 150, 40)); d.point((9, 0), fill=(255, 220, 120))   # flame on top
    return im

ICONS = {"chest": chest, "door": door, "smelter": smelter, "map": map_, "house": house, "anvil": anvil, "shield": shield, "pouch": pouch,
         "fire": fire, "eye": eye, "hammer": hammer, "gem": gem, "coins": coins}

if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    done = [finish(f(), n, out) for n, f in ICONS.items()]
    sheet = Image.new("RGBA", (len(done) * 92, 92), (58, 50, 44, 255))
    for i, im in enumerate(done):
        big = im.resize((80, 80), Image.NEAREST)
        sheet.paste(big, (i * 92 + 6, 6), big)
    sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST).save(os.path.join(out, "_sheet.png"))
