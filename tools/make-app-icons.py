#!/usr/bin/env python3
"""앱 아이콘 굽기 — 원본 그림 하나에서 Windows `.ico`와 맥 `.icns`를 만든다.

    python3 tools/make-app-icons.py

원본은 `src/Vn.App/Assets/app-icon.png`(1024 × 1024 · 투명 바탕) 하나다. 그림을 바꾸려면 원본을 갈고 이 스크립트를 다시 돌린다 —
`.ico` · `.icns`를 손으로 고치지 않는다. Pillow가 있어야 한다(`pip install pillow`).

- `app.ico`  — 창 · 실행 파일(Windows). 16~256 여덟 크기. 256은 PNG로, 그보다 작은 것은 BMP로 담는다(탐색기 · 작업 표시줄이 옛 방식으로
               읽는 자리가 있어, 작은 크기를 PNG로 담으면 그림이 비는 Windows가 있다).
- `app.icns` — 맥 꾸러미(`publish-macos.sh`). 16~1024.
"""
import io
import struct
import sys
from pathlib import Path

from PIL import Image

ASSETS = Path(__file__).resolve().parent.parent / "src" / "Vn.App" / "Assets"
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 256]
# 맥의 칸 이름 → 한 변(px). @2x 칸은 같은 그림을 두 배 크기로 담는다.
ICNS_SLOTS = [
    (b"icp4", 16), (b"icp5", 32), (b"icp6", 64), (b"ic07", 128), (b"ic08", 256), (b"ic09", 512), (b"ic10", 1024),
    (b"ic11", 32), (b"ic12", 64), (b"ic13", 256), (b"ic14", 512),
]


def png_bytes(image: Image.Image) -> bytes:
    out = io.BytesIO()
    image.save(out, format="PNG", optimize=True)
    return out.getvalue()


def bmp_frame(image: Image.Image) -> bytes:
    """ICO 안의 BMP 한 장 — 32비트 BGRA(아래 줄부터) 뒤에 1비트 가림막(AND 마스크)."""
    size = image.width
    pixels = image.tobytes("raw", "BGRA")
    row = size * 4
    flipped = b"".join(pixels[y * row:(y + 1) * row] for y in range(size - 1, -1, -1))
    mask_row = ((size + 31) // 32) * 4   # 줄마다 4바이트 경계로
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, len(flipped) + mask_row * size, 0, 0, 0, 0)
    return header + flipped + bytes(mask_row * size)   # 가림막은 비운다 — 투명은 알파가 정한다


def write_ico(master: Image.Image, path: Path) -> None:
    frames = []
    for size in ICO_SIZES:
        image = master.resize((size, size), Image.LANCZOS)
        frames.append((size, png_bytes(image) if size >= 256 else bmp_frame(image)))

    out = io.BytesIO()
    out.write(struct.pack("<HHH", 0, 1, len(frames)))
    offset = 6 + 16 * len(frames)
    for size, data in frames:
        edge = 0 if size >= 256 else size   # 256은 0으로 적는다
        out.write(struct.pack("<BBBBHHII", edge, edge, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    for _, data in frames:
        out.write(data)
    path.write_bytes(out.getvalue())


def write_icns(master: Image.Image, path: Path) -> None:
    body = b""
    for name, size in ICNS_SLOTS:
        data = png_bytes(master.resize((size, size), Image.LANCZOS))
        body += name + struct.pack(">I", len(data) + 8) + data
    path.write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)


def main() -> int:
    source = ASSETS / "app-icon.png"
    master = Image.open(source).convert("RGBA")
    if master.size != (1024, 1024):
        print(f"원본은 1024 × 1024여야 합니다 — 지금 {master.width} × {master.height}: {source}", file=sys.stderr)
        return 1

    write_ico(master, ASSETS / "app.ico")
    write_icns(master, ASSETS / "app.icns")
    print(f"구웠습니다: {ASSETS / 'app.ico'} · {ASSETS / 'app.icns'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
