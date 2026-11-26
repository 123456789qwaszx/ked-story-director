#!/usr/bin/env python3
"""테마의 선 아이콘 글꼴을 굽는다 — src/Vn.App/Themes/PastelSky/Fonts/StoryDirectorIcons.ttf (themes).

화면의 아이콘(📂 ⚙ ⌂ ⑂ 📄 ⚠ …)은 글자다. PC의 글꼴이 그리면 색 · 굵기 · 크기가 제각각이라, 테마가 그 글자들의
그림을 직접 싣는다 — 같은 굵기의 선 그림으로. 테마의 글꼴 목록(Tokens.axaml의 UiFont · DisplayFont) 끝에 이 글꼴을
두면, 본문 글꼴에 없는 그 글자들만 여기서 그려진다(글자색 · 글자 크기를 그대로 따른다). 화면 코드와 문구 표는 그대로다.

그림을 고치거나 더하려면 아래 ICONS를 고치고 다시 굽는다:  python tools/make-theme-icons.py   (fonttools 필요)
그림은 24 × 24 모눈(y는 아래로)에 선으로 적는다 — ("line", 점들) · ("closed", 점들) · ("fill", 점들).
"""
import math
import os
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, "..", "src", "Vn.App", "Themes", "PastelSky", "Fonts")
BODY = os.path.join(FONTS, "GowunDodum-Regular.ttf")   # 줄 높이를 본문 글꼴과 맞춘다
OUT = os.path.join(FONTS, "StoryDirectorIcons.ttf")

UPM = 1000
STROKE = 2.0          # 모눈 단위의 선 굵기
BOX_BOTTOM = -130     # 24 모눈이 놓이는 자리(글자 상자의 아래 · 위) — 한글 한 자의 높이와 맞춘다
SCALE = 1000 / 24
SIDE = 40             # 양옆 여백


def arc(cx, cy, r, a0, a1, n=14):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def ring(cx, cy, r, n=20):
    return ("closed", arc(cx, cy, r, 0, 360 - 360 / n, n - 1))


def spokes(cx, cy, r0, r1, count, offset=0):
    out = []
    for i in range(count):
        a = math.radians(offset + 360 * i / count)
        out.append(("line", [(cx + r0 * math.cos(a), cy + r0 * math.sin(a)), (cx + r1 * math.cos(a), cy + r1 * math.sin(a))]))
    return out


ICONS = {
    0x2302: [("line", [(3.5, 11), (12, 3.5), (20.5, 11)]), ("line", [(5.5, 9.8), (5.5, 20), (18.5, 20), (18.5, 9.8)])],              # ⌂ 장면 시작
    0x2442: [("line", [(7, 4), (7, 20)]), ("line", [(7, 14)] + arc(7, 4, 10, 90, 20, 8)[1:]), ring(17, 6.5, 1.6, 10)],               # ⑂ 분기
    0x2699: [ring(12, 12, 3, 14), ring(12, 12, 6.6, 24)] + spokes(12, 12, 6.6, 9.4, 8, 22.5),                                         # ⚙ 설정
    0x1F4C2: [("closed", [(3, 6.5), (9.5, 6.5), (11.5, 9), (21, 9), (21, 19), (3, 19)])],                                             # 📂 폴더
    0x1F4C4: [("closed", [(6, 3), (14, 3), (18.5, 7.5), (18.5, 21), (6, 21)]), ("line", [(14, 3), (14, 7.5), (18.5, 7.5)]),
              ("line", [(9, 12.5), (15.5, 12.5)]), ("line", [(9, 16.5), (15.5, 16.5)])],                                              # 📄 에피소드
    0x26A0: [("closed", [(12, 3.5), (21.5, 20), (2.5, 20)]), ("line", [(12, 9.5), (12, 14)]), ("line", [(12, 17), (12, 17.2)])],     # ⚠ 경고
    0x2715: [("line", [(6, 6), (18, 18)]), ("line", [(18, 6), (6, 18)])],                                                             # ✕ 닫기
    0x21AA: [("line", [(5, 5), (5, 10)] + arc(9, 10, 4, 180, 90, 6)[1:] + [(19, 14)]), ("line", [(15, 10), (19, 14), (15, 18)])],     # ↪ 들렀다 돌아옴
    0x21BA: [("line", arc(12, 12.5, 7.5, -130, 150, 20)), ("line", [(11.5, 2.5), (7.2, 6.8), (13, 8.4)])],                            # ↺ 되돌리기
    0x2304: [("line", [(6, 9), (12, 15), (18, 9)])],                                                                                  # ⌄ 펼치기
    0x25BE: [("fill", [(6.5, 9), (17.5, 9), (12, 16.5)])],                                                                            # ▾
    0x25B8: [("fill", [(9, 6.5), (16.5, 12), (9, 17.5)])],                                                                            # ▸
    0x1F512: [("closed", [(5.5, 11), (18.5, 11), (18.5, 20.5), (5.5, 20.5)]), ("line", [(8, 11), (8, 8)] + arc(12, 8, 4, 180, 360, 8)[1:] + [(16, 11)])],   # 🔒 잠김
    0x25A2: [("closed", arc(16, 8, 3, -90, 0, 4) + arc(16, 16, 3, 0, 90, 4) + arc(8, 16, 3, 90, 180, 4) + arc(8, 8, 3, 180, 270, 4))],                      # ▢
    0x2702: [ring(6.5, 7, 2.6, 12), ring(6.5, 17, 2.6, 12), ("line", [(8.6, 8.6), (20, 18)]), ("line", [(8.6, 15.4), (20, 6)])],     # ✂ 끊기
}


def to_font(point):
    x, y = point
    return (round(SIDE + x * SCALE), round(BOX_BOTTOM + (24 - y) * SCALE))


def clockwise(points):
    area = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))
    return points if area < 0 else points[::-1]


def contours(shapes):
    out = []
    half = STROKE / 2
    for kind, points in shapes:
        if kind == "fill":
            out.append(points)
            continue
        segments = list(zip(points, points[1:])) + ([(points[-1], points[0])] if kind == "closed" else [])
        for (x0, y0), (x1, y1) in segments:
            length = math.hypot(x1 - x0, y1 - y0)
            if length > 1e-6:
                nx, ny = -(y1 - y0) / length * half, (x1 - x0) / length * half
                out.append([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)])
        for (x, y) in points:   # 둥근 이음 · 둥근 끝
            out.append([(x + half * math.cos(2 * math.pi * i / 12), y + half * math.sin(2 * math.pi * i / 12)) for i in range(12)])
    return out


def glyph(shapes):
    pen = TTGlyphPen(None)
    for contour in contours(shapes):
        points = clockwise([to_font(p) for p in contour])
        pen.moveTo(points[0])
        for point in points[1:]:
            pen.lineTo(point)
        pen.closePath()
    drawn = pen.glyph()
    if drawn.numberOfContours > 0:
        drawn.flags[0] |= 0x40   # 겹치는 윤곽(OVERLAP_SIMPLE) — 선마다 한 조각씩이라 서로 겹친다
    return drawn


def main():
    body = TTFont(BODY)
    k = UPM / body["head"].unitsPerEm
    ascent, descent = round(body["hhea"].ascent * k), round(body["hhea"].descent * k)

    names = [".notdef"] + [f"u{code:04X}" for code in ICONS]
    builder = FontBuilder(UPM, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({code: f"u{code:04X}" for code in ICONS})
    glyphs = {".notdef": TTGlyphPen(None).glyph()}
    glyphs.update({f"u{code:04X}": glyph(shapes) for code, shapes in ICONS.items()})
    builder.setupGlyf(glyphs)
    advance = round(24 * SCALE) + SIDE * 2
    builder.setupHorizontalMetrics({name: (advance, 0) for name in names})
    builder.setupHorizontalHeader(ascent=ascent, descent=descent)
    builder.setupNameTable({"familyName": "Story Director Icons", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=ascent, sTypoDescender=descent, usWinAscent=ascent, usWinDescent=-descent)
    builder.setupPost()
    builder.save(OUT)
    print(f"{OUT} — {len(ICONS)} icons")


if __name__ == "__main__":
    main()
