from pathlib import Path
import struct
import zlib
from dbpf import Key, fnv64

ROOT = Path(__file__).resolve().parent.parent
NAMES = ('Fairy', 'Ghost', 'Genie', 'Mermaid', 'Vampire', 'Werewolf')
STATUS_SPECIES = ('Witch', 'Fairy', 'Vampire', 'Werewolf', 'Genie', 'Mermaid')
STATUS_ICONS = tuple((species, kind) for species in STATUS_SPECIES for kind in ('Skill', 'Power'))
STATUS_NAMES = tuple(species + kind for species, kind in STATUS_ICONS)
OCCULT_ICONS = ROOT / 'assets' / 'UI' / 'Icons' / 'Occult'
HUD_FILES = {name: ('Vampire.png' if name == 'Vampire' else name + 'HUD.png') for name in NAMES}
PALETTE = (
    ('Witch',    0xFF4D63D3),
    ('Fairy',    0xFF1ED561),
    ('Vampire',  0xFF6629C6),
    ('Werewolf', 0xFFEC2233),
    ('Genie',    0xFFF9AD00),
    ('Mermaid',  0xFF0098DF),
    ('Ghost',    0xFF2CE0ED),
)

BAR_TRACK = 0xFF263158
BAR_SATURATION = 0.70
BAR_HUE = {'Witch': 250.0}
BAR_KEEP_DISTANCE = 60.0
BAR_TEXTURE = 0.97


def _lab(rgb):
    def linear(value):
        value /= 255.0
        return ((value + 0.055) / 1.055) ** 2.4 if value > 0.04045 else value / 12.92
    r, g, b = (linear(v) for v in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883
    def curve(t):
        return t ** (1 / 3.0) if t > 0.008856 else 7.787 * t + 16 / 116.0
    fx, fy, fz = curve(x), curve(y), curve(z)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))


def _rgb(color):
    return ((color >> 16) & 255, (color >> 8) & 255, color & 255)


def bar_distance(tint):
    a = _lab(tuple(round(v * BAR_TEXTURE) for v in _rgb(tint)))
    b = _lab(tuple(round(v * BAR_TEXTURE) for v in _rgb(BAR_TRACK)))
    return sum((p - q) ** 2 for p, q in zip(a, b)) ** 0.5


def bar_tint(name, accent):
    import colorsys
    if name not in BAR_HUE and bar_distance(accent) >= BAR_KEEP_DISTANCE:
        return accent
    hue, saturation, _ = colorsys.rgb_to_hsv(*(v / 255.0 for v in _rgb(accent)))
    hue = BAR_HUE[name] / 360.0 if name in BAR_HUE else hue
    red, green, blue = colorsys.hsv_to_rgb(hue, max(saturation, BAR_SATURATION), 1.0)
    return 0xFF000000 | (round(red * 255) << 16) | (round(green * 255) << 8) | round(blue * 255)


BAR_TINTS = tuple((name, bar_tint(name, color)) for name, color in PALETTE)

IMAGE = 0x2F7D0004
LIFE_ART_SIZE = 128
LIFE_ART = tuple((name, OCCULT_ICONS / name / 'Lifestate' / (name + 'Lifestate.png')) for name, _ in PALETTE)
GENERATED = ROOT / 'build' / 'generated' / 'OccultAssetData.g.cs'


def key(name):
    return Key(IMAGE, 0, fnv64('SupernaturalExpanded.HudIcon.' + name))


def status_key(name):
    return Key(IMAGE, 0, fnv64('SupernaturalExpanded.StatusIcon.' + name))


def life_art_key(species):
    return Key(IMAGE, 0, fnv64('SupernaturalExpanded.LifeStateArt.' + species))


def validate_life_art(data, species):
    if data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR':
        raise ValueError('Life State art is not a PNG: ' + species)
    width, height, depth, color = struct.unpack_from('>IIBB', data, 16)
    if (width, height, depth, color) != (LIFE_ART_SIZE, LIFE_ART_SIZE, 8, 6):
        raise ValueError('Life State art must be the original %dx%d 8-bit RGBA PNG: %s' % (LIFE_ART_SIZE, LIFE_ART_SIZE, species))
    if zlib.crc32(data[12:29]) & 0xffffffff != struct.unpack_from('>I', data, 29)[0]:
        raise ValueError('Life State art IHDR CRC mismatch: ' + species)


def present_life_art():
    return [species for species, path in LIFE_ART if path.is_file()]

def validate_png(data):
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError('HUD icon is not PNG')
    offset, chunks, compressed = 8, [], bytearray()
    interlace = 0
    while offset < len(data):
        size, = struct.unpack_from('>I', data, offset)
        kind = data[offset + 4:offset + 8]
        body = data[offset + 8:offset + 8 + size]
        crc, = struct.unpack_from('>I', data, offset + 8 + size)
        if zlib.crc32(kind + body) & 0xffffffff != crc:
            raise ValueError('HUD PNG CRC mismatch')
        chunks.append(kind)
        if kind == b'IHDR':
            width, height, depth, color, compression, filtering, interlace = struct.unpack('>IIBBBBB', body)
            if (width, height, depth, color, compression, filtering) != (24, 16, 8, 6, 0, 0) or interlace not in (0, 1):
                raise ValueError('HUD icons must be 24x16 8-bit RGBA PNGs')
        if kind == b'IDAT':
            compressed.extend(body)
        offset += size + 12
    if offset != len(data) or chunks[0] != b'IHDR' or chunks[-1] != b'IEND' or chunks.count(b'IHDR') != 1:
        raise ValueError('Invalid HUD PNG structure')
    pixels = zlib.decompress(compressed)
    passes = ((0, 0, 1, 1),) if interlace == 0 else ((0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2))
    cursor = 0
    for x, y, dx, dy in passes:
        columns = len(range(x, 24, dx))
        if not columns:
            continue
        for _ in range(y, 16, dy):
            if cursor >= len(pixels) or pixels[cursor] > 4:
                raise ValueError('Invalid HUD PNG scanline')
            cursor += 1 + columns * 4
    if cursor != len(pixels):
        raise ValueError('Invalid HUD PNG pixel data length')


def resources():
    result = []
    for name in NAMES:
        data = (OCCULT_ICONS / name / 'HUD' / HUD_FILES[name]).read_bytes()
        validate_png(data)
        result.append((key(name), data))
    if len({k for k, _ in result}) != len(NAMES):
        raise ValueError('HUD icon key collision')
    return result


def status_resources():
    result = []
    for species, kind in STATUS_ICONS:
        name = species + kind
        data = (OCCULT_ICONS / species / 'Skills' / (name + '.png')).read_bytes()
        if data[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Status icon is not PNG: ' + name)
        result.append((status_key(name), data))
    if len({key for key, _ in result}) != len(STATUS_NAMES):
        raise ValueError('Status icon key collision')
    return result

def life_state_resources():
    result = []
    for species, path in LIFE_ART:
        if path.is_file():
            data = path.read_bytes()
            validate_life_art(data, species)
            result.append((life_art_key(species), data))
    return result


def source():
    rows = [

        'using System;',
        'using Sims3.SimIFace;',
        'using Sims3.UI.Hud;',
        '',
        'namespace SupernaturalExpanded.Journal',
        '{',
        '    internal static class OccultAssetData',
        '    {',
        '        internal const OccultTypes SupportedTypes =',
        '            ' + ' | '.join('OccultTypes.' + name for name, _ in PALETTE) + ';',
        '',
        '        private static readonly OccultTypes[] Order =',
        '        {',
        '            ' + ', '.join('OccultTypes.'+name for name, _ in PALETTE),
        '        };',
        '',
        '        private static readonly uint[] Accents =',
        '        {',
        '            ' + ', '.join('0x%08X' % color for _, color in PALETTE),
        '        };',
        '',

        '        private static readonly uint[] BarTints =',
        '        {',
        '            ' + ', '.join('0x%08X' % color for _, color in BAR_TINTS),
        '        };',
        '',
        '        internal static OccultTypes Primary(OccultTypes types)',
        '        {',
        '            foreach (OccultTypes type in Order)',
        '            {',
        '                if ((types & type) != OccultTypes.None)',
        '                {',
        '                    return type;',
        '                }',
        '            }',
        '            return OccultTypes.None;',
        '        }',
        '',
        '        internal static uint Accent(OccultTypes primary)',
        '        {',
        '            int index = Array.IndexOf(Order, primary);',
        '            return index < 0 ? 0u : Accents[index];',
        '        }',
        '',
        '        internal static uint BarTint(OccultTypes primary)',
        '        {',
        '            int index = Array.IndexOf(Order, primary);',
        '            return index < 0 ? 0u : BarTints[index];',
        '        }',
        '',
    ]
    for name in NAMES:
        rows.append('        internal const ulong %s = 0x%016Xul;' % (name, key(name).instance))
    for name in STATUS_NAMES:
        rows.append('        internal const ulong %sStatus = 0x%016Xul;' % (name, status_key(name).instance))
    present = present_life_art()
    for species in present:
        rows.append('        internal const ulong %sLifeStateArt = 0x%016Xul;' % (species, life_art_key(species).instance))
    rows += [
        '',

        '        internal static ulong LifeStateArt(OccultTypes type)',
        '        {',
    ]
    if present:
        rows.append('            switch (type)')
        rows.append('            {')
        for species in present:
            rows.append('                case OccultTypes.%s: return %sLifeStateArt;' % (species, species))
        rows.append('                default: return 0ul;')
        rows.append('            }')
    else:
        rows.append('            return 0ul;')
    rows += [
        '        }',
        '',
        '        internal static ulong StatusIcon(OccultTypes type, bool power)',
        '        {',
        '            switch (type)',
        '            {',
    ]
    for species in STATUS_SPECIES:
        rows.append('                case OccultTypes.%s: return power ? %sPowerStatus : %sSkillStatus;' % (species, species, species))
    rows += [
        '                default: return 0ul;',
        '            }',
        '        }',
        '',
        '        internal static bool TryGetIcon(OccultTypes type, out ResourceKey key)',
        '        {',
        '            switch (type)',
        '            {',
    ]
    for name in NAMES:
        rows.append('                case OccultTypes.%s: key = new ResourceKey(%s, 0x2F7D0004, 0); return true;' % (name, name))
    rows += [
        '                default: key = default(ResourceKey); return false;',
        '            }',
        '        }',
        '    }',
        '}',
        '',
    ]
    return '\n'.join(rows)


def generate(destination=GENERATED):
    resources()
    status_resources()
    life_state_resources()
    for species, path in LIFE_ART:
        if not path.is_file():
            print('NOTE: %s Life State art not found (%s); the Journal keeps its small icon.' % (species, path.relative_to(ROOT)))
    if len({name for name, _ in PALETTE}) != len(PALETTE) or len(PALETTE) != 7:
        raise ValueError('Duplicate or missing palette entries')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(source().encode('utf-8'))


if __name__ == '__main__':
    generate()
