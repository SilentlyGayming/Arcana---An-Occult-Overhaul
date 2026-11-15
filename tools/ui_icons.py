"""Journal Grimoire resources from the four untouched original PNGs.
The native-font question-mark footer no longer uses a custom image.
"""
import struct
import zlib
from pathlib import Path

from dbpf import fnv64
from ui_resources import (halo_resources, TAB_BODY_INSTANCE, TAB_CLEAR_INSTANCE,
    tab_body_png, tab_clear_png, TABLE_CORNER_INSTANCE_LEFT,
    TABLE_CORNER_INSTANCE_RIGHT, table_corner_left_png, table_corner_right_png)


ROOT = Path(__file__).resolve().parent.parent
HELP_ICON_NAME = "SupernaturalExpanded.UI.QuestionMark"
HELP_ICON_INSTANCE = fnv64(HELP_ICON_NAME)
HELP_ICON_SOURCE = ROOT / "assets" / "UI" / "Icons" / "Interface" / "QuestionMark_Blue_64x64.png"
_PNG_SIG = b"\x89PNG\r\n\x1a\n"


def _chunk(kind, payload):
    return (struct.pack(">I", len(payload)) + kind + payload
            + struct.pack(">I", zlib.crc32(kind + payload) & 0xFFFFFFFF))


def _white_mask(png):
    """Losslessly preserve a source RGBA PNG's alpha, making RGB tintable white.

    Implements the standard PNG scanline filters using Python's stdlib.  This
    deliberately checks the source's format instead of quietly resizing or
    re-encoding unexpected artwork.
    """
    if not png.startswith(_PNG_SIG):
        raise ValueError("QuestionMark_Blue_64x64.png must be a PNG")
    offset = 8
    image_data = bytearray()
    ihdr = None
    while offset + 12 <= len(png):
        size = struct.unpack_from(">I", png, offset)[0]
        kind = png[offset + 4:offset + 8]
        data = png[offset + 8:offset + 8 + size]
        if len(data) != size:
            raise ValueError("Truncated question-mark PNG")
        if kind == b"IHDR":
            ihdr = data
        elif kind == b"IDAT":
            image_data.extend(data)
        elif kind == b"IEND":
            break
        offset += size + 12
    if ihdr is None or len(ihdr) != 13:
        raise ValueError("Question-mark PNG lacks IHDR")
    width, height, depth, color_type, compression, filter_method, interlace = struct.unpack(">IIBBBBB", ihdr)
    if (width, height, depth, color_type, compression, filter_method, interlace) != (64, 64, 8, 6, 0, 0, 0):
        raise ValueError("Question-mark image must be original 64x64 non-interlaced RGBA")
    original = zlib.decompress(bytes(image_data))
    stride = width * 4
    if len(original) != height * (1 + stride):
        raise ValueError("Unexpected PNG scanline size")

    # Undo source filters to recover precisely the original alpha channel.
    previous = bytearray(stride)
    output = bytearray()
    offset = 0
    for _ in range(height):
        method = original[offset]
        offset += 1
        line = bytearray(original[offset:offset + stride])
        offset += stride
        if method > 4:
            raise ValueError("Unsupported PNG filter method")
        for x in range(stride):
            left = line[x - 4] if x >= 4 else 0
            above = previous[x]
            upper_left = previous[x - 4] if x >= 4 else 0
            if method == 1:
                prediction = left
            elif method == 2:
                prediction = above
            elif method == 3:
                prediction = (left + above) // 2
            elif method == 4:
                p = left + above - upper_left
                distances = (abs(p - left), abs(p - above), abs(p - upper_left))
                prediction = (left, above, upper_left)[distances.index(min(distances))]
            else:
                prediction = 0
            line[x] = (line[x] + prediction) & 255
        previous = line.copy()
        # Keep the precise alpha mask, including anti-aliased edge pixels.
        for x in range(0, stride, 4):
            line[x:x + 3] = b"\xff\xff\xff"
        output.append(0)  # Unfiltered output scanline
        output.extend(line)
    return _PNG_SIG + _chunk(b"IHDR", ihdr) + _chunk(b"IDAT", zlib.compress(bytes(output), 9)) + _chunk(b"IEND", b"")


# These are the four user-selected source PNGs already supplied with Arcana.
# Keep the original pixel data and alpha; there is no resizing or recoloring here.
GRIMOIRE_CATEGORY_NAMES = ("Spells", "Curses", "Rituals", "Abilities")
GRIMOIRE_ICON_DIR = ROOT / "assets" / "UI" / "Icons" / "Interface" / "Grimoire" / "Grimoire"


def grimoire_icon_instance(category):
    if category not in GRIMOIRE_CATEGORY_NAMES:
        raise ValueError("Unknown Grimoire category: " + str(category))
    return fnv64("SupernaturalExpanded.UI.Grimoire.Category." + category)


def resources():
    result = []  # Native text help glyph; no mask resource needed
    for category in GRIMOIRE_CATEGORY_NAMES:
        source = GRIMOIRE_ICON_DIR / (category + ".png")
        data = source.read_bytes()
        if not data.startswith(_PNG_SIG):
            raise ValueError("Grimoire category artwork must be a PNG: " + str(source))
        result.append((grimoire_icon_instance(category), data))
    result.extend(halo_resources())
    result.append((TAB_BODY_INSTANCE, tab_body_png()))
    result.append((TAB_CLEAR_INSTANCE, tab_clear_png()))
    result.append((TABLE_CORNER_INSTANCE_LEFT, table_corner_left_png()))
    result.append((TABLE_CORNER_INSTANCE_RIGHT, table_corner_right_png()))
    return result
