import math
import struct
import zlib
from pathlib import Path

from dbpf import fnv64

ROOT = Path(__file__).resolve().parent.parent
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


def rgba_png(width, height, pixel_at):
    def chunk(kind, data):
        crc = zlib.crc32(kind + data) & 0xffffffff
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", crc)

    rows = bytearray()
    for y in range(height):
        rows.append(0)
        for x in range(width):
            rows.extend(pixel_at(x, y))
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b"")


HALO_FACE_WIDTH = 29
HALO_FACE_HEIGHT = 24
HALO_PAD = 1.6
HALO_DENSITY = 4
HALO_WIDTH = math.ceil((HALO_FACE_WIDTH + 2 * HALO_PAD) * HALO_DENSITY)
HALO_HEIGHT = math.ceil((HALO_FACE_HEIGHT + 2 * HALO_PAD) * HALO_DENSITY)
HALO_INSTANCE = fnv64("SupernaturalExpanded.UI.PerkHalo.r75")


def halo_pixel(x, y):
    radius = 4.0
    dx = abs((x + .5) / HALO_WIDTH - .5) * (HALO_FACE_WIDTH + 2 * HALO_PAD) - HALO_FACE_WIDTH / 2 + radius
    dy = abs((y + .5) / HALO_HEIGHT - .5) * (HALO_FACE_HEIGHT + 2 * HALO_PAD) - HALO_FACE_HEIGHT / 2 + radius
    distance = math.hypot(max(0, dx), max(0, dy)) + min(max(dx, dy), 0) - radius
    if distance < -.75 or distance >= HALO_PAD:
        return (255, 211, 95, 0)
    core = math.exp(-.5 * (distance / .36)**2)
    haze = 1 - max(0, distance) / HALO_PAD
    alpha = max(0, min(1, (distance + .75) / .4)) * (.74 * core + .23 * haze * haze)
    return (255, round(193 + 42 * core), round(61 + 85 * core), round(255 * alpha))


def halo_resources():
    return [(HALO_INSTANCE, rgba_png(HALO_WIDTH, HALO_HEIGHT, halo_pixel))]


STAR_INSTANCE = fnv64("SupernaturalExpanded.UI.PerkSelectionStarSpin.r75")
STAR_FRAMES = 12
STAR_SIZE = 32


def star_png(frame):
    rotation = -math.pi / 2 + math.tau * frame / (5 * STAR_FRAMES)
    center = STAR_SIZE / 2
    points = []
    for i in range(10):
        angle = rotation + i * math.pi / 5
        radius = STAR_SIZE * (0.47 if i % 2 == 0 else 0.23)
        points.append((center + radius * math.cos(angle), center + radius * math.sin(angle)))
    edges = list(zip(points, points[1:] + points[:1]))

    def inside(x, y):
        return sum((y1 > y) != (y2 > y) and x < x1 + (x2 - x1) * (y - y1) / (y2 - y1)
                   for (x1, y1), (x2, y2) in edges) % 2

    def pixel(x, y):
        coverage = sum(inside(x + ox, y + oy) for ox in (.25, .75) for oy in (.25, .75))
        return (255, 255, 255, round(255 * coverage / 4))

    return rgba_png(STAR_SIZE, STAR_SIZE, pixel)


def star_resources():
    return [(STAR_INSTANCE + frame, star_png(frame)) for frame in range(STAR_FRAMES)]


GLITTER_BODY_INSTANCE = fnv64("SupernaturalExpanded.UI.RankGlitter.Body.r75")
GLITTER_TIP_INSTANCE = fnv64("SupernaturalExpanded.UI.RankGlitter.Tip.r75")
GLITTER_WIDTH, GLITTER_PERIOD = 36, 180
GLITTER_STARS = ((.31, 3, 1.1), (.66, 19, .7), (.43, 33, .7), (.55, 48, 1.25),
                 (.76, 63, .65), (.27, 74, .75), (.48, 89, 1.15), (.69, 100, .65),
                 (.34, 114, .8), (.54, 125, 1.05), (.77, 141, .7), (.24, 153, .7),
                 (.41, 165, 1.1), (.63, 175, .7))


def glitter_body(x, y):
    y = (y + .5) % GLITTER_PERIOD
    u = (x + .5) / GLITTER_WIDTH
    edge = math.sin(math.pi * u) ** 1.6
    ribbon = math.sin(math.tau * (y / GLITTER_PERIOD + u * .38))
    shine = (.19 + .08 * ribbon) * edge
    sparkle = 0
    for sx, sy, scale in GLITTER_STARS:
        dx = abs(u - sx) * GLITTER_WIDTH
        dy = min(abs(y - sy), GLITTER_PERIOD - abs(y - sy))
        core = math.exp(-.5 * ((dx / (1.6 * scale))**2 + (dy / (2.6 * scale))**2))
        arms = math.exp(-.5 * ((dx / (.7 * scale))**2 + (dy / (5.1 * scale))**2))
        sparkle = max(sparkle, .72 * core + .28 * arms)
    return (round(145 + 94 * sparkle), round(142 + 103 * sparkle), 255,
            round(255 * min(.95, shine + .74 * sparkle)))


def glitter_tip(x, y):
    dx, dy = abs((x + .5) / GLITTER_WIDTH - .5), abs((y + .5) / GLITTER_WIDTH - .5)
    core = math.exp(-.5 * ((dx / .085)**2 + (dy / .075)**2))
    rays = max(math.exp(-.5 * ((dx / .036)**2 + (dy / .23)**2)),
               math.exp(-.5 * ((dx / .24)**2 + (dy / .032)**2)))
    mask = max(0, 1 - 2 * max(dx, dy))
    return (round(181 + 74 * core), round(181 + 74 * core), 255,
            round(255 * min(1, .4 * rays + .82 * core) * mask))


def glitter_resources():
    return [(GLITTER_BODY_INSTANCE, rgba_png(GLITTER_WIDTH, GLITTER_PERIOD, glitter_body)),
            (GLITTER_TIP_INSTANCE, rgba_png(GLITTER_WIDTH, GLITTER_WIDTH, glitter_tip))]


MASK_SOURCE = "stock"
MASK_STOCK_KEY = "69e704d5dd095e39"
MASK_SIZE = 64
MASK_RADIUS = 8
MASK_CUSTOM_NAME = "SupernaturalExpanded.UI.PortraitMask"
_MASK_SAMPLES = 8


def portrait_mask_key():
    return MASK_STOCK_KEY if MASK_SOURCE == "stock" else f"{fnv64(MASK_CUSTOM_NAME):016x}"


def _mask_coverage(x, y):
    inside = 0
    for sy in range(_MASK_SAMPLES):
        for sx in range(_MASK_SAMPLES):
            px = x + (sx + 0.5) / _MASK_SAMPLES
            py = y + (sy + 0.5) / _MASK_SAMPLES
            cx = min(max(px, MASK_RADIUS), MASK_SIZE - MASK_RADIUS)
            cy = min(max(py, MASK_RADIUS), MASK_SIZE - MASK_RADIUS)
            if (px - cx) ** 2 + (py - cy) ** 2 <= MASK_RADIUS ** 2:
                inside += 1
    return inside / (_MASK_SAMPLES * _MASK_SAMPLES)


def portrait_mask_png():
    def pixel(x, y):
        alpha = round(255 * _mask_coverage(x, y))
        value = 255 if alpha else 0
        return (value, value, value, alpha)

    return rgba_png(MASK_SIZE, MASK_SIZE, pixel)


def portrait_mask_resources():
    return [] if MASK_SOURCE == "stock" else [(fnv64(MASK_CUSTOM_NAME), portrait_mask_png())]


TILE_EDGE_NAME = "SupernaturalExpanded.UI.PerkTileEdge.r2"
TILE_EDGE_INSTANCE = fnv64(TILE_EDGE_NAME)
TILE_EDGE_WIDTH, TILE_EDGE_HEIGHT = 84, 90
TILE_EDGE_DENSITY = 2
TILE_EDGE_RADIUS = 6.0
TILE_EDGE_INSET = 1.0
TILE_EDGE_RING = 2.5
TILE_EDGE_GLOW = 5.0
TILE_EDGE_GLOW_PEAK = 0.4
TILE_EDGE_DEPTH = TILE_EDGE_RING + TILE_EDGE_GLOW


def tile_edge_alpha(x, y):
    cx = min(max(x, TILE_EDGE_RADIUS), TILE_EDGE_WIDTH - TILE_EDGE_RADIUS)
    cy = min(max(y, TILE_EDGE_RADIUS), TILE_EDGE_HEIGHT - TILE_EDGE_RADIUS)
    corner = math.hypot(x - cx, y - cy)
    if corner > 0:
        depth = TILE_EDGE_RADIUS - corner
    else:
        depth = min(x, TILE_EDGE_WIDTH - x, y, TILE_EDGE_HEIGHT - y)
    depth -= TILE_EDGE_INSET
    if depth < 0 or depth >= TILE_EDGE_DEPTH:
        return 0.0
    if depth < TILE_EDGE_RING:
        return 1.0
    return TILE_EDGE_GLOW_PEAK * (1 - (depth - TILE_EDGE_RING) / TILE_EDGE_GLOW) ** 2


def tile_edge_png():
    width, height = TILE_EDGE_WIDTH * TILE_EDGE_DENSITY, TILE_EDGE_HEIGHT * TILE_EDGE_DENSITY

    def pixel(column, row):
        alpha = sum(tile_edge_alpha((column + ox) / TILE_EDGE_DENSITY, (row + oy) / TILE_EDGE_DENSITY)
                    for ox in (.25, .75) for oy in (.25, .75)) / 4
        return (255, 255, 255, round(255 * alpha))

    return rgba_png(width, height, pixel)


GRIMOIRE_CATEGORY_NAMES = ("Spells", "Charms", "Curses", "Rituals", "Abilities")
GRIMOIRE_ICON_DIR = ROOT / "assets" / "UI" / "Icons" / "Interface" / "Grimoire"


def grimoire_icon_instance(category):
    if category not in GRIMOIRE_CATEGORY_NAMES:
        raise ValueError("Unknown Grimoire category: " + str(category))
    return fnv64("SupernaturalExpanded.UI.Grimoire.Category." + category)


def grimoire_resources():
    result = []
    for category in GRIMOIRE_CATEGORY_NAMES:
        source = GRIMOIRE_ICON_DIR / (category + ".png")
        data = source.read_bytes()
        if not data.startswith(PNG_SIGNATURE):
            raise ValueError("Grimoire category artwork must be a PNG: " + str(source))
        result.append((grimoire_icon_instance(category), data))
    return result


def resources():
    return (grimoire_resources() + halo_resources() + [(TILE_EDGE_INSTANCE, tile_edge_png())]
            + portrait_mask_resources() + star_resources() + glitter_resources())
