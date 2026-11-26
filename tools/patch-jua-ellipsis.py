#!/usr/bin/env python3
"""「파스텔 하늘」의 제목 글꼴을 굽는다 — Jua에 말줄임표(… U+2026) 한 글자를 더한 사본 (themes).

Jua에는 말줄임표가 없다. 카드 제목이 잘릴 때 붙는 말줄임표는 그 글의 글꼴만으로 그려지므로(다른 글꼴로 넘어가지 않는다)
□로 보였다. Jua의 마침표 셋을 나란히 놓은 글자를 더하고, 고친 글꼴임이 드러나게 이름을 「Jua Story Director」로 바꾼다
(SIL OFL 1.1이 허용하는 수정 — THIRD-PARTY-NOTICES.txt의 9 · B-2에 적는다).

  python tools/patch-jua-ellipsis.py 원본/Jua-Regular.ttf      (원본: https://github.com/google/fonts/tree/main/ofl/jua · fonttools 필요)
"""
import os
import sys
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "src", "Vn.App", "Themes", "PastelSky", "Fonts", "JuaStoryDirector-Regular.ttf")
FAMILY = "Jua Story Director"


def main(source):
    font = TTFont(source)
    cmap = font.getBestCmap()
    period = cmap[0x2E]
    advance, bearing = font["hmtx"][period]

    pen = TTGlyphPen(font.getGlyphSet())
    for index in range(3):
        pen.addComponent(period, (1, 0, 0, 1, advance * index, 0))

    name = "ellipsis"
    font.setGlyphOrder(font.getGlyphOrder() + [name])
    font["glyf"][name] = pen.glyph()
    font["hmtx"][name] = (advance * 3, bearing)
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[0x2026] = name

    for record in font["name"].names:
        if record.nameID in (1, 16):
            record.string = FAMILY
        elif record.nameID == 4:
            record.string = FAMILY + " Regular"
        elif record.nameID == 6:
            record.string = "JuaStoryDirector-Regular"
        elif record.nameID == 3:
            record.string = "JuaStoryDirector-Regular;modified from Jua (added U+2026)"

    font.save(OUT)
    print(f"{OUT} — U+2026 added")


if __name__ == "__main__":
    main(sys.argv[1])
