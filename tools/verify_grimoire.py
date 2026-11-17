"""Read-only static check for the Journal layout and reusable Grimoire list wiring.

This cannot establish TS3 runtime event behavior or native control appearance.
"""
import re
import struct
import xml.etree.ElementTree as ET
import zlib
from collections import Counter
from functools import lru_cache
from pathlib import Path
import ui_resources
from journal_layouts import (layouts, GRIMOIRE_FIRST_TAB, GRIMOIRE_FIRST_ICON,
    GRIMOIRE_FIRST_CAPTION, GRIMOIRE_LIST_PANEL, GRIMOIRE_LIST_BODY,
    GRIMOIRE_DESC_PANEL, GRIMOIRE_DESC_BODY, GRIMOIRE_DESC_NAME, GRIMOIRE_DESC_TEXT,
    GRIMOIRE_DESC_PROMPT, GRIMOIRE_DESC_META, GRIMOIRE_LIST_TITLE, GRIMOIRE_TIER_HEADER, GRIMOIRE_COST_HEADER,
    GRIMOIRE_FIRST_COLUMN_BUTTON, GRIMOIRE_FIRST_SORT_ARROW, GRIMOIRE_FIRST_SORT_ARROW_DOWN,
    SORT_ASCENDING_ARROW, FRAME_MARGINS, HEADER_CELL, LIST_BODY, SCROLL_TRACK, SCROLL_THUMB as THUMB_KEY,
    SCROLL_UP_GLOW, SCROLL_DOWN_GLOW, SCROLL_THUMB_GLOW, ROW_ALTERNATE, COLLECTION_FRAME,
    SORT_DESCENDING_ARROW, SCROLL_UP_ARROW, SCROLL_DOWN_ARROW,
    GRIMOIRE_FIRST_SEPARATOR, GRIMOIRE_FIRST_ROW, GRIMOIRE_SCROLL_TRACK,
    GRIMOIRE_SCROLL_THUMB, GRIMOIRE_SCROLL_UP, GRIMOIRE_SCROLL_DOWN,
    PORTRAIT, PORTRAIT_FRAME, PORTRAIT_BORDER, CONTENT, SURROUND, GRIMOIRE_ROWS,
    TITLE, SIM_NAME, OCCULT_RANK, STATUS_PANEL, WITCH,
    LIST_OUTLINE, DIALOG_BUTTON, NAVY, FIRST_TAB, FOOTER_FONT, STATUS_TRACK,
    GRIMOIRE_FIRST_TABLE_EDGE, GRIMOIRE_FIRST_TABLE_CORNER, DIVIDER, DIVIDER_FILL, TABLE_EDGE,
    PERK_PULSE, RANK_GLITTER_TILES, SPARK_PARTICLES,
    ENTRY_GLYPH, HUD_BUTTON, PURCHASE_FLIGHT)


import check_game_api
import cs_source
import hud_icons
import localization

from ui_icons import GRIMOIRE_CATEGORY_NAMES, grimoire_icon_instance, resources

@lru_cache(maxsize=1)
def english():
    return localization.catalogs()[localization.ENGLISH]


def check_text(source, key, expected):
    assert f'"{key}"' in source and english()[key] == expected, key


def check_localization():
    import json
    from tempfile import TemporaryDirectory
    from dbpf import fnv64

    # Golden vector uses the hash returned by SimIFace.ResourceUtils.HashString64.
    unicode_catalog = {'Arcana.Test.Unicode': 'é🌙'}
    expected = bytes.fromhex('5354424c02000001000000000000000000'
                             'ac38be81a8b337e003000000e9003cd819df')
    assert localization.encode_table(unicode_catalog) == expected
    assert localization.read_table(expected) == {0xE037B3A881BE38AC: 'é🌙'}
    title = 'Arcana.Journal.Title'
    tier_cost = 'Arcana.Journal.Grimoire.Details.TierAndCost'
    rest = 'Arcana.Witch.Perk.RestForTheWicked.Description.1'
    with TemporaryDirectory() as temporary:
        directory = Path(temporary)
        en = directory / 'en-US' / 'strings.json'
        fr = directory / 'fr-FR' / 'strings.json'
        for path in (en, fr):
            path.parent.mkdir()
        en.write_text(json.dumps(english()), encoding='utf-8')
        english_key = localization.table_key('en-US')
        french_key = localization.table_key('fr-FR')
        assert french_key.instance == english_key.instance | (7 << 56)
        english_table = localization.encode_table(english())
        assert localization.resources(directory) == [(english_key, english_table)]
        assert localization.encode_table(dict(reversed(list(english().items())))) == english_table

        partial = {title: 'Journal surnaturel', tier_cost: 'Énergie : {1} / niveau : {0}',
                   rest: '{name} retrouve son énergie en dormant.'}
        fr.write_text(json.dumps(partial, ensure_ascii=False), encoding='utf-8')
        packed = dict(localization.resources(directory))
        assert list(packed) == [english_key, french_key]
        assert localization.read_table(packed[french_key]) == {fnv64(key): text for key, text in partial.items()}

        for invalid, error in (
            ('{"' + title + '": "A", "' + title + '": "B"}', 'duplicate key'),
            (json.dumps({title: 'A', 'Arcana.Journal.title': 'B'}), 'FNV64 collision'),
            (json.dumps({title: '   '}), 'nonempty text'),
            (json.dumps({'Arcana.Unknown.Key': 'B'}), 'unrecognized key'),
            (json.dumps({tier_cost: 'Énergie : {0}'}), 'requires placeholders'),
            (json.dumps({title: '{0:X}'}), 'unsupported placeholder'),
            (json.dumps({rest: '{Name}'}), 'requires placeholders'),
        ):
            fr.write_text(invalid, encoding='utf-8')
            try:
                localization.catalogs(directory)
            except ValueError as caught:
                assert error in str(caught), str(caught)
            else:
                raise AssertionError(f'Accepted invalid localization data: {invalid}')
        fr.unlink()
        en.write_text(json.dumps(dict(english(), **{tier_cost: 'Tier: {0}'})), encoding='utf-8')
        try:
            localization.catalogs(directory)
        except ValueError as caught:
            assert 'incompatible placeholders' in str(caught), str(caught)
        else:
            raise AssertionError('Accepted an English format missing a runtime argument')
    print('PASS: STBL golden vector, deterministic UTF-16 encoding, partial locale and placeholder, key and collision validation')


def generated_rgba(png):
    """Decode the unfiltered, single-IDAT RGBA emitted by ui_resources."""
    assert png[:16] == b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR'
    width, height, depth, mode, compression, filtering, interlace = struct.unpack_from('>IIBBBBB', png, 16)
    assert (depth, mode, compression, filtering, interlace) == (8, 6, 0, 0, 0)
    size, tag = struct.unpack_from('>I4s', png, 33)
    assert tag == b'IDAT'
    samples = zlib.decompress(png[41:41 + size])
    stride = 1 + 4 * width
    assert len(samples) == height * stride and not any(samples[::stride])
    pixels = {
        (x, y): tuple(samples[y * stride + 1 + 4 * x:y * stride + 5 + 4 * x])
        for y in range(height) for x in range(width)
    }
    return (width, height), pixels


def overview_text(root_dir):
    types = ('OverviewTip', 'OverviewRow', 'OverviewData', 'OverviewContext',
             'IOverviewProvider', 'OverviewProviders', 'WitchRankDescriptions', 'OverviewIcons')
    return '\n'.join(cs_source.class_text(root_dir, name) for name in types)


def main():
    root_dir = Path(__file__).resolve().parents[1]
    check_localization()
    constants = (root_dir/'src'/'Core'/'NativeLayouts.cs').read_text()
    name = re.search(r'Journal = "(\w+)"', constants).group(1)
    assert name.endswith('_r78'), 'Layout revision was not bumped'
    layout = dict(layouts())[name]
    root = ET.fromstring(layout)
    counts = Counter(int(p.get('value'),16) for p in
        (node.find("prop[@name='ControlID']") for node in root.iter('object'))
        if p is not None)
    assert all(v == 1 for v in counts.values()), 'Duplicate control ID'
    for i, name in enumerate(GRIMOIRE_CATEGORY_NAMES):
        assert counts[GRIMOIRE_FIRST_TAB+i] == 1
        assert counts[GRIMOIRE_FIRST_ICON+i] == 1
        assert counts[GRIMOIRE_FIRST_CAPTION+i] == 1
        assert f'{grimoire_icon_instance(name):016x}'.encode() in layout
    for cid in (GRIMOIRE_LIST_PANEL, GRIMOIRE_LIST_BODY, GRIMOIRE_DESC_PANEL, GRIMOIRE_DESC_BODY,
        GRIMOIRE_DESC_NAME, GRIMOIRE_DESC_TEXT, GRIMOIRE_DESC_PROMPT, GRIMOIRE_DESC_META, GRIMOIRE_LIST_TITLE, GRIMOIRE_TIER_HEADER, GRIMOIRE_COST_HEADER,
        GRIMOIRE_SCROLL_TRACK, GRIMOIRE_SCROLL_THUMB,
        GRIMOIRE_SCROLL_UP, GRIMOIRE_SCROLL_DOWN, PORTRAIT, PORTRAIT_FRAME):
        assert counts[cid] == 1, f'Missing {cid:08X}'
    for i in range(3):
        assert counts[GRIMOIRE_FIRST_COLUMN_BUTTON+i] == 1
        assert counts[GRIMOIRE_FIRST_SORT_ARROW+i] == 1
        assert counts[GRIMOIRE_FIRST_SORT_ARROW_DOWN+i] == 1
    for key in (SORT_ASCENDING_ARROW, SORT_DESCENDING_ARROW, SCROLL_UP_ARROW[0], SCROLL_DOWN_ARROW[0]):
        assert layout.count(key.encode()) >= 1, f'Sprite {key} missing from layout'
    assert 0x53E00075 not in counts, 'Upper-right page label still exists'
    assert 'TabName' not in constants, 'TabName constant still present'
    for i in range(2):
        assert counts[GRIMOIRE_FIRST_SEPARATOR+i] == 1
        assert counts[GRIMOIRE_FIRST_TABLE_EDGE+i] == 1
        assert counts[GRIMOIRE_FIRST_TABLE_CORNER+i] == 1
    for i in range(GRIMOIRE_ROWS):
        for delta in range(5):
            assert counts[GRIMOIRE_FIRST_ROW+i*5+delta] == 1
    for deleted in (0x53E000D3, 0x53E000D5, 0x53E000D6, 0x53E00144, 0x53E00145):
        assert counts[deleted] == 0, f'Obsolete control {deleted:08X} still exists'
    for name in GRIMOIRE_CATEGORY_NAMES:
        assert dict(resources())[grimoire_icon_instance(name)].startswith(b'\x89PNG\r\n\x1a\n')
    assert PORTRAIT_BORDER.encode() in layout
    mask = ui_resources.portrait_mask_key()
    portrait = [o for o in root.iter('object')
                if (o.find("prop[@name='ControlID']") is not None
                    and int(o.find("prop[@name='ControlID']").get('value'), 16) == PORTRAIT)][0]
    drawn = portrait.find(".//prop[@name='DrawMask']")
    assert drawn is not None and drawn.get('key') == f'2f7d0004:00000000:{mask}', 'Portrait mask missing'
    assert portrait.find(".//prop[@name='Image']") is None, 'Portrait must take its image at run time'
    frame = [o for o in root.iter('object')
             if (o.find("prop[@name='ControlID']") is not None
                 and int(o.find("prop[@name='ControlID']").get('value'), 16) == PORTRAIT_FRAME)][0]
    assert frame.find(".//prop[@name='GlowType']").get('value') == '1', 'Portrait glow must be always-on'
    custom = dict(ui_resources.portrait_mask_resources())
    assert ui_resources.MASK_SOURCE in ('stock', 'custom')
    assert bool(custom) == (ui_resources.MASK_SOURCE == 'custom'), 'Mask resource and source disagree'
    assert b'0xff7397c6' not in layout, 'Unwanted square portrait matte remains'
    for phrase in (b'Select an entry to view its details', b'Filter By...', b'Details'):
        assert phrase not in layout
    cs=cs_source.file_text(root_dir, 'GrimoirePage')
    assert 'SetEntries(int index, IEnumerable<GrimoireEntry> incoming)' in cs
    assert 'StringComparer.OrdinalIgnoreCase' in cs and 'a.TierOrder.CompareTo' in cs
    assert 'a.EnergyCost.CompareTo' in cs
    assert 'MouseWheel += OnWheel' in cs
    assert '\u25b2' not in cs and '\u25bc' not in cs, 'Unicode arrows remain in GrimoirePage'
    assert 'FillColor' in cs and 'rowBackgrounds[i].ShadeColor' not in cs
    assert 'entries[i].Clear();' in cs
    known = {int(p.get('value'), 16) for _, text in layouts() for node in ET.fromstring(text).iter('object')
             for p in [node.find("prop[@name='ControlID']")] if p is not None}
    for match in re.finditer(r'internal const uint (\w+) = 0x(53E[0-9A-Fa-f]{5});', constants):
        assert int(match.group(2), 16) in known, f'NativeLayouts.{match.group(1)} is not in any layout'
    assert 'GetChildByIndex(9)' not in cs_source.class_text(root_dir, 'JournalTheme'), 'Index-based donor lookup remains'
    assert 'System.Reflection' not in cs and 'GetProperty(' not in cs, 'Reflection remains in GrimoirePage'
    for call in ('UIManager.SetCaptureTarget(InputContext.kICMouse', 'UIManager.ReleaseCapture(InputContext.kICMouse',
                 'args.MouseWheelDelta', 'UIManager.GetCursorPosition()', 'ScreenToWindow('):
        assert call in cs, f'Missing {call}'
    assert SORT_ASCENDING_ARROW == '424ae4f366bbd963' and SORT_DESCENDING_ARROW == '5cdfce63208c1e46'
    test_file = cs_source.file_path(root_dir, 'GrimoireTestData')
    test = test_file.read_text().strip()
    assert test.startswith('#if ARCANA_GRIMOIRE_TEST') and test.endswith('#endif'), 'Test data is not fenced'
    window = cs_source.class_text(root_dir, 'JournalWindow')
    assert '#if ARCANA_GRIMOIRE_TEST\n                GrimoireTestData.Apply(grimoirePage);\n#endif' in window.replace('\r\n', '\n')
    build = (root_dir/'tools'/'build_journal.py').read_text()
    assert build.count('ARCANA_GRIMOIRE_TEST') == 1 and 'if args.grimoire_test:' in build
    assert 'ARCANA_GRIMOIRE_TEST' not in (root_dir/'build.bat').read_text(), 'build.bat must not enable test mode'
    for source in (root_dir/'src').rglob('*.cs'):
        assert 'ARCANA_GRIMOIRE_TEST' not in source.read_text().replace('#if ARCANA_GRIMOIRE_TEST', '') or source == test_file, source
    check_phase_c(root, layout, cs, root_dir, constants)
    check_perk_feedback(root, root_dir, constants)
    check_perk_navigation(root_dir, window)
    check_journal_eligibility(root_dir, window)
    check_overview(root, root_dir, constants, counts)
    check_r57(root, root_dir, constants, counts, layout)
    check_r62(root, root_dir, layout)
    check_r63(root, root_dir, layout)
    check_r72(root, root_dir, layout)
    print('PASS: every 0x53E control constant exists in a packaged layout; no index-based donor lookup')
    print('PASS: no reflection in GrimoirePage; typed wheel/cursor/capture calls present')
    print('PASS: sample entries compile only with --grimoire-test, which build.bat does not pass')
    print('PASS: four categories / 3 fixed column headers / 12 scrollable row slots')
    print('PASS: no dropdown, no Details heading and no square portrait matte')
    print('PASS: four untouched icon PNG resources, native portrait frame')
    print('PASS: sorting/data-source/wheel hooks present in C# source')
    print('Separate checks: compile with build_journal.py and verify rendering and input in The Sims 3.')
def check_r63(root, root_dir, layout):
    """r63: one window size for every page, shared content region, page-specific native frames, taller Grimoire."""
    import journal_layouts as jl
    src = root_dir/'src'
    window = cs_source.class_text(root_dir, 'JournalWindow')
    geometry = cs_source.file_text(root_dir, 'WindowGeometry')
    frames = cs_source.class_text(root_dir, 'JournalFrames')
    theme = cs_source.class_text(root_dir, 'JournalTheme')
    overview = cs_source.class_text(root_dir, 'OverviewPage')
    grimoire = cs_source.file_text(root_dir, 'GrimoirePage')
    perks = cs_source.file_text(root_dir, 'PowerPage')

    # One geometry definition: the Perks page's 1000x860 window, declared once and read by every page.
    assert geometry.count('class WindowGeometry') == 1
    assert (constant(geometry, 'Width'), constant(geometry, 'Height')) == (1000.0, 860.0), 'The Perks size is the window size'
    assert 'WindowWidth' not in geometry and 'WindowHeight' not in geometry, 'The Perks layout reads WindowGeometry, not a size of its own'
    assert constant(geometry, 'ContentHeight') == constant(geometry, 'ContentBottom') - constant(geometry, 'ContentTop')
    for text, label in ((window, 'JournalWindow'), (overview, 'OverviewPage'), (grimoire, 'GrimoirePage')):
        assert 'WindowGeometry.' in text, f'{label} reads the shared geometry'
    for stale in ('GrimoireHeight', 'PageSize(', 'ShowsPerks', 'OverviewPage.WindowWidth', 'OverviewPage.WindowHeight', 'GrimoirePage.ContentBottom'):
        assert stale not in window + overview + grimoire + perks, f'{stale} was a per-page size'
    assert not re.search(r'const float (WindowWidth|WindowHeight)\b', overview + grimoire), 'Overview and Grimoire declare no size of their own'
    assert window.count('WindowGeometry.Width') == 1 and window.count('WindowGeometry.Height') == 1, 'The outer size is chosen in exactly one place'
    assert 'Place(content, WindowGeometry.ContentLeft, WindowGeometry.ContentTop, WindowGeometry.ContentWidth, WindowGeometry.ContentHeight);' in window
    place = window.split('private void PlaceWindow()')[1].split('private void LayoutActivePage()')[0]
    assert place.count('root.Area =') == 1 and 'GetFitScale(width, height)' in place and 'ActiveSelection' not in place and 'page ==' not in place, 'The outer window does not depend on the page'
    assert 'if (windowPlaced && fit == scale && showHeader == placedHeader)' in place, 'A page switch skips the window placement while nothing it depends on changed'
    active = window.split('private void LayoutActivePage()')[1].split('private Rect Scaled')[0]
    assert 'root.Area' not in active and 'root.Position' not in active and 'header.' not in active, 'The page layout never touches the window or header'
    update = window.split('private void UpdateLayout()')[1].split('private void PlaceWindow()')[0]
    assert update.index('PlaceWindow();') < update.index('LayoutActivePage();'), 'The window is placed before the page layout'
    assert 'windowPlaced = false;' in window.split('private void OnViewportChanged')[1].split('private static float GetFitScale')[0], 'A viewport change always places the window again'
    assert 'overviewPage.Layout();' in window and 'grimoirePage.Layout();' in window

    # Page frames: both native shells are captured once, with their own slicing, and swapped without rebuilding the window.
    assert 'private JournalFrames frames;' in window and 'frames = JournalTheme.ApplyDialog(root, tabs, grimoirePage.Tabs);' in window
    assert theme.index('frames.CaptureSingleNotch();') < theme.index('if (StyleTwoButtonFrame(journal))') < theme.index('frames.CaptureTwoNotch();'), 'The single-notch face is captured before the two-notch donor replaces it'
    for member in ('ScaleType', 'ScaleAmount', 'ScaleArea'):
        assert frames.count(f'face.{member} = source.{member};') == 1 and frames.count(f'target.{member} = face.{member};') == 1, f'{member} travels with each face'
    assert 'target[(DrawableBase.ControlStates)i] = face.Images[i];' in frames and 'journal.Invalidate();' in frames
    assert 'Show(bool wantTwoNotch)' in frames and 'wantTwoNotch && twoNotch != null ? twoNotch : single' in frames, 'Missing two-notch artwork falls back to the single-notch frame'
    assert 'new Dialog' not in frames and 'Resize' not in frames and 'LoadLayout' not in frames and '.Area' not in frames.replace('target.ScaleArea', '').replace('face.ScaleArea', '').replace('source.ScaleArea', '') and 'Position' not in frames, 'The swap never rebuilds, resizes or moves the window'
    select = window.split('private void SelectPage(int index)')[1].split('private void OnKnowledgeChanged')[0]
    assert select.index('bool twoNotch = page == JournalPages.Perks;') < select.index('cancel.Visible = twoNotch;') < select.index('frames.Show(twoNotch);'), 'The X and the two-notch frame come from the same boolean'
    assert 'float width = WindowGeometry.Width;' in place and 'float height = WindowGeometry.Height;' in place
    assert not re.search(r'JournalPages|page ==|ActiveSelection|showPerks', place.split('Place(title')[0]), 'The outer window uses the shared size'
    assert jl.COLLECTION_FRAME == '163462cf3324a2a8' and '0x54D0A9831E5754E7UL' in theme, 'Single-notch Collection frame and two-notch donor'
    assert 'Place(close, width - CloseCenterX' in window and 'Place(cancel, width - CancelCenterX' in window, 'The checkmark is placed from the same constants whichever frame shows'
    assert 'CloseMode.Confirm' in window and '.Commit(' not in select and '.Discard(' not in select, 'Page changes never touch staged perks'

    # Grimoire: a taller list from the same row pool, sized by one constant on the C# side and one on the layout side.
    rows = constant_int(grimoire, 'VisibleRows')
    assert rows == jl.GRIMOIRE_ROWS == 12
    assert 0x53E00100 + 5 * (rows - 1) + 4 < jl.GRIMOIRE_SCROLL_TRACK, 'The row control IDs stop short of the scrollbar parts'
    assert 'for (uint row = 0; row < GrimoirePage.VisibleRows; row++)' in theme, 'Every Grimoire row gets the skin ink'
    pane_bottom = constant(grimoire, 'PaneTop') + constant(grimoire, 'PaneHeight')
    description = constant(grimoire, 'DescriptionHeight')
    assert description >= 100 and pane_bottom + constant(grimoire, 'DescriptionGap') + description + constant(grimoire, 'BottomPad') == constant(geometry, 'ContentBottom'), 'The description panel ends at the content region'
    assert constant(grimoire, 'PaneTop') + constant(grimoire, 'PaneHeight') < constant(geometry, 'ContentBottom')
    assert 'WindowGeometry.ContentWidth - 2f * WindowGeometry.ContentInset' in grimoire and 'WindowGeometry.ContentWidth - 2f * WindowGeometry.ContentInset' in overview, 'Overview and Grimoire share one content width'
    print('PASS: r63: one %dx%d window for every page, content region (%d,%d)-(%d,%d); single- and two-notch frames captured with their own slicing and swapped on page change with the X; Grimoire shows %d rows'
          % (constant(geometry, 'Width'), constant(geometry, 'Height'), constant(geometry, 'ContentLeft'), constant(geometry, 'ContentTop'),
             constant(geometry, 'ContentRight'), constant(geometry, 'ContentBottom'), rows))


def check_r69(root_dir, window, theme):
    """r69: Perks uses the original D58F144D5B0FE27A two-button frame with each donor's own slicing; no experimental frame remains."""
    import journal_layouts as jl
    sources = ''.join(path.read_text() for path in (root_dir / 'src').rglob('*.cs'))
    for gone in ('PerksFrame', 'SetHoursFrame', 'FriendSearchFrame', 'EditCollectionsFrame',
                 '0x8383C130D3F928E1', '0x9E62DF13DA920738', '0x1F6A7CAEE479BF4C', '0x9A238F5239B8A36D'):
        assert gone not in sources, gone + ' belonged to an experimental frame'
    donors = theme.split('private static readonly ulong[] TwoButtonLayouts =')[1].split('};')[0]
    layouts = re.findall(r'0x([0-9A-F]{16})UL', donors)
    exports = [int(value, 0) for value in re.search(r'TwoButtonExports = \{([^}]*)\}', theme).group(1).split(',')]
    assert layouts == ['54D0A9831E5754E7', 'B73E807A1250DC6F', 'BD8616686C83937B'] and exports == [1, 1, 0x10000], 'Only the three D58F144D5B0FE27A donors, the 686x636 one first'

    slicing = {'54D0A9831E5754E7': (0.5,) * 4, 'B73E807A1250DC6F': (0.5,) * 4, 'BD8616686C83937B': (0.3,) * 4}
    offsets = []
    for layout_id, export in zip(layouts, exports):
        graph = ET.fromstring((root_dir / 'references' / 'TwoButtonNative' / (layout_id + '.layout')).read_bytes())
        dialog = next(node for node in graph if node.get('cls') == 'Dialog')
        assert int(dialog.get('id'), 16) == export, 'The export id is the donor dialog root'
        drawable = dialog.find("prop[@name='DialogDrawable']/object")
        keys = [value.get('key') for value in drawable.findall("prop[@name='Image']/value") if value.get('key')]
        assert [key.lower() for key in keys] == ['2f7d0004:00000000:d58f144d5b0fe27a'], 'One image: D58F144D5B0FE27A'
        assert drawable.find("prop[@name='ScaleType']").get('value') == '2' and drawable.find("prop[@name='ScaleFactor']").get('value') == '1,1'
        assert [float(x.get('value')) for x in drawable.findall("prop[@name='ScaleArea']/struct/prop")] == list(slicing[layout_id]), 'The donor keeps its own nine-slice margins'
        buttons = {}
        for node in dialog.findall(".//object[@cls='Sims3IconButton']"):
            buttons[node.find("prop[@name='Comment']").get('value')] = [round(float(v), 2) for v in node.find("prop[@name='Area']").get('value').split(',')]
        accept, cancel = buttons['Accept'], buttons['Cancel']
        assert (accept[2] - accept[0], accept[3] - accept[1]) == (56, 56) and (cancel[2] - cancel[0], cancel[3] - cancel[1]) == (29, 29)
        centers = (-(accept[0] + accept[2]) / 2, -(accept[1] + accept[3]) / 2, -(cancel[0] + cancel[2]) / 2, -(cancel[1] + cancel[3]) / 2)
        ours = (constant(window, 'CloseCenterX'), constant(window, 'FooterCenterY'), constant(window, 'CancelCenterX'), constant(window, 'FooterCenterY'))
        assert all(abs(a - b) <= t for a, b, t in zip(centers, ours, (1, 1, 8, 1))), 'Checkmark stays at donor center; X shifts slightly left for optical separation'
        offsets.append(max(abs(a - b) for a, b in zip(centers, ours)))

    assert jl.COLLECTION_FRAME == '163462cf3324a2a8', 'Overview and Grimoire keep the Collection Journal frame'
    body = theme.split('private static bool StyleTwoButtonFrame(Dialog journal)')[1].split('private static void StyleCancel(')[0]
    assert 'CopyFace(target, source);' in body and 'CopyImages(target, source);' not in body, 'Image and slicing come from the same donor'
    print('PASS: r69: Perks frame is D58F144D5B0FE27A from donor 54D0A9831E5754E7 (export 1, nine-slice 0.5 on every side) with B73E807A1250DC6F and BD8616686C83937B as fallbacks, each with its own slicing; no experimental frame remains; checkmark native and X optically shifted left (max center offset %.1f)' % max(offsets))


def check_r70(root, root_dir, theme):
    """r70: Collection (Overview, Grimoire) and D58F (Perks) are cut with the same native slicing, each taking artwork and slicing together from its native layout."""
    def native(folder, layout_id):
        graph = ET.fromstring((root_dir / 'references' / folder / (layout_id + '.layout')).read_bytes())
        return next(node for node in graph if node.get('cls') == 'Dialog')

    def slicing(dialog, image):
        drawable = dialog.find("prop[@name='DialogDrawable']/object")
        keys = [value.get('key').lower() for value in drawable.findall("prop[@name='Image']/value") if value.get('key')]
        assert keys == ['2f7d0004:00000000:' + image], 'One image: ' + image
        return (drawable.find("prop[@name='ScaleType']").get('value'), drawable.find("prop[@name='ScaleFactor']").get('value'),
                tuple(float(x.get('value')) for x in drawable.findall("prop[@name='ScaleArea']/struct/prop")))

    collection = slicing(native('CollectionJournal', '57560E847AC1F2F1'), '163462cf3324a2a8')
    perks = slicing(native('TwoButtonNative', '54D0A9831E5754E7'), 'd58f144d5b0fe27a')
    generated = slicing(root.find('object'), '163462cf3324a2a8')
    assert collection == perks == generated == ('2', '1,1', (0.5,) * 4), 'ScaleType, ScaleFactor and ScaleArea match on both native frames and the generated fallback'

    apply = theme.split('internal static JournalFrames ApplyDialog(')[1].split('StyleCancel(journal);')[0]
    assert apply.count('CopyFace(DialogFrame(journal), DialogFrame(donor));') == 1 and 'CopyImages(' not in apply, 'The Collection frame takes its slicing with its artwork'
    assert theme.count('CopyImages(') == 2 and 'if (CopyImages(target, source))' in theme.split('private static void CopyFace(')[1].split('private static bool CopyImages(')[0], 'CopyImages is only the artwork step of CopyFace'
    odd = [layout for layout in ('B73E807A1250DC6F', 'BD8616686C83937B') if slicing(native('TwoButtonNative', layout), 'd58f144d5b0fe27a') != collection]
    print('PASS: r70: Collection frame 163462CF3324A2A8 and Perks frame D58F144D5B0FE27A (donor 54D0A9831E5754E7) both ScaleType 2, ScaleFactor 1,1, ScaleArea 0.5 on every side; the generated fallback frame matches; the Collection frame is taken with CopyFace')
    print('INFO: fallback donor(s) %s slice D58F differently and are only used if the first two donors fail to load' % ', '.join(odd))


def check_r62(root, root_dir, layout):
    """r62: footer button page rules, tab group geometry, Knowledge typography and icon quality."""
    import struct
    import journal_layouts as jl
    src = root_dir/'src'
    window = cs_source.class_text(root_dir, 'JournalWindow')
    lst = cs_source.class_text(root_dir, 'OverviewList')
    geo = cs_source.file_text(root_dir, 'WindowGeometry')

    # The X belongs to the Perks page; changing pages never commits or discards, and Escape discards from any page.
    select = window.split('private void SelectPage(int index)')[1].split('private void OnKnowledgeChanged')[0]
    assert 'bool twoNotch = page == JournalPages.Perks;' in select and 'cancel.Visible = twoNotch;' in select, 'X shown only while Perks is selected'
    assert '.Commit(' not in select and '.Discard(' not in select and 'Close(' not in select, 'Switching pages keeps staged picks untouched'
    assert 'cancel.Visible = true' not in window and 'close.Visible = false' not in window, 'The checkmark stays visible on every page'
    trigger = window.split('case CancelTrigger:')[1].split('break;')[0]
    assert 'Close(CloseMode.Cancel, true);' in trigger and 'page' not in trigger.lower().replace('args', ''), 'Escape discards from every page'
    assert window.count('Close(CloseMode.Cancel') == 2 and 'Close(CloseMode.Confirm, keyActivation);' in window
    assert 'IsNavigable(Button button)' in window and 'button.Visible && button.Enabled' in window and 'if (!IsNavigable(keyboardButton))' in select, 'A hidden X is skipped by the keyboard'

    # Tabs: the group fits left of the status panel, with the same size and top for every tab.
    left, gap = constant(window, 'TabLeft'), constant(window, 'TabGap')
    widest = 3 * 137 + 2 * gap
    assert left + widest <= 896 - constant(geo, 'RightMargin') - constant(geo, 'PanelWidth') + 12, 'Three tabs end under the identity block'
    assert 'tabLeft[i] = left;' in window and 'tabWidth[i] = width;' in window

    # Knowledge rows: Bold 13pt names and counts (the same stock style as the Sim name), icons at or below the Grimoire icon art's size.
    assert jl.OV_KNOWLEDGE_FONT == '0x062eae07' == jl.IDENTITY_FONT
    first = jl.OV_LIST_FIRST + jl.OV_KNOWLEDGE_LIST * jl.OV_LIST_STRIDE
    for row in range(jl.OV_LIST_ROWS[jl.OV_KNOWLEDGE_LIST]):
        base = first + row * jl.OV_ROW_CONTROLS
        assert layout_font(root, base + 1) == layout_font(root, base + 3) == jl.OV_KNOWLEDGE_FONT, 'Names and counts share one font'
    for name in jl.GRIMOIRE_CATEGORY_NAMES:
        icon = root_dir/'assets'/'UI'/'Icons'/'Interface'/'Grimoire'/'Grimoire'/(name + '.png')
        width, height = struct.unpack('>II', icon.read_bytes()[16:24])
        assert width == height and constant(lst, 'MaxIconSize') <= width, f'{name}: icons are never drawn larger than their {width}px art'
    assert 'Math.Floor(nextRow * IconShare)' in lst and 'journal.Place(icons[i], listLeft + Padding, y + (float)Math.Floor((rowHeight - iconSize) / 2f), iconSize, iconSize);' in lst, 'Square icons, aspect ratio kept'
    assert 'KnowledgeRows = 8' in cs_source.class_text(root_dir, 'OverviewPage') and 'rows.Count' in lst, 'The row count comes from the data, not a fixed four'
    assert 'root.Area = NativeLayouts.Box(0, 0, (float)Math.Round(width * scale), (float)Math.Round(height * scale));' in window and 'root.Position = new Vector2((float)Math.Round(root.Area.TopLeft.x), top);' in window, 'The window sits on whole pixels so glyphs are not drawn between pixels'
    assert 'identity_glow' not in (root_dir/'tools'/'journal_layouts.py').read_text() + (root_dir/'tools'/'ui_icons.py').read_text(), 'The old rectangular glow is gone'
    print('PASS: r62: X only on Perks (Escape and Confirm unchanged), tab group inset %d with %d gaps, Knowledge uses Bold 13pt and Grimoire art at or below 60px' % (left, gap))


def check_r57(root, root_dir, constants, counts, layout):
    """r57: native Cancel, selected-tab body, Fairy and rank handling, shared Knowledge rows, Knowledge row sizing."""
    import math
    import journal_layouts as jl
    src = root_dir/'src'
    window = cs_source.class_text(root_dir, 'JournalWindow')
    page = cs_source.file_text(root_dir, 'PowerPage')
    selection = cs_source.class_text(root_dir, 'PowerSelection')
    status = cs_source.file_text(root_dir, 'OccultStatusProvider')
    rank = cs_source.class_text(root_dir, 'OccultRankLabel')
    data = overview_text(root_dir)
    overview = cs_source.class_text(root_dir, 'OverviewPage')
    lst = cs_source.class_text(root_dir, 'OverviewList')
    faces = cs_source.class_text(root_dir, 'TabFaces')
    order = child_order(root)

    # 1. Native Cancel (X): X glyph + the checkmark's reliably visible circular frame.
    assert counts[jl.CANCEL] == 1 and counts[jl.CLOSE] == 1
    cancel = ET.tostring(control(root, jl.CANCEL), encoding='unicode')
    for key in jl.CHECK_STATES:
        assert key in cancel, f'Checkmark-derived circular Cancel face {key}'
    assert jl.CANCEL_GLYPH in cancel and jl.CANCEL_GLYPH == 'a11c98cfe0beff5a', 'The stock X glyph'
    assert jl.CHECK_GLOW in cancel, 'The stock glow mask'
    assert {o.get('cls') for o in control(root, jl.CANCEL).iter('object')} <= {'Sims3IconButton', 'IconButtonMultiDrawable', 'StdDrawable', 'IconDrawable', 'GlowModulate'}, 'Only stock drawables, no custom-drawn X'
    assert 'Cancel = 0x53E0000D;' in constants
    theme = cs_source.class_text(root_dir, 'JournalTheme')
    assert 'StyleCancel(journal);' in theme and 'CopyFace(Frame(cancel.Drawable), Frame(checkmark.Drawable));' in theme, 'Cancel copies native check circle from Confirm'
    assert 'cancel.ShadeColor = checkmark.ShadeColor;' in theme and 'cancel.Invalidate();' in theme, 'Cancel adopts the working native skin and redraws'
    assert 'PartyPicker' not in theme, 'Do not overwrite the working checkmark face with the unreliable PartyPicker face'
    assert 'Bind(cancel, RequestCancel);' in window and window.count('cancel.Visible') == 2 and 'cancel.Visible = false;' in window and 'bool twoNotch = page == JournalPages.Perks;' in window and 'cancel.Visible = twoNotch;' in window, 'The X is visible on the Perks page only'
    assert 'Place(close, width - CloseCenterX - CloseSize / 2f, height - FooterCenterY - CloseSize / 2f, CloseSize, CloseSize);' in window
    assert 'Place(cancel, width - CancelCenterX - CancelSize / 2f, height - FooterCenterY - CancelSize / 2f, CancelSize, CancelSize);' in window
    assert constant(window, 'CloseSize') == 56, 'The checkmark keeps its native size'
    assert constant(window, 'CancelSize') == jl.CANCEL_SIZE == 29 and constant(window, 'CancelSize') < constant(window, 'CloseSize'), 'Preserve the r74 native 29-unit X and its larger checkmark'
    assert constant(window, 'CloseCenterX') == 41 and constant(window, 'FooterCenterY') == 41 and constant(window, 'CancelCenterX') == 79.5, 'Both buttons align to the exact stock two-button donor centres'
    assert abs((94 - 29 / 2.0) - constant(window, 'CancelCenterX')) <= 0.01
    # Verify the game LAYO donor saved with this source release, rather than trusting
    # resource IDs mentioned only in comments. The image itself remains a native EA resource.
    donor_xml = root_dir / 'references' / 'TwoButtonNative' / '54D0A9831E5754E7.layout'
    if not donor_xml.is_file():
        print('SKIP: references/TwoButtonNative/54D0A9831E5754E7.layout is not in this source drop; the donor LAYO geometry was not re-checked')
    else:
        donor_graph = ET.fromstring(donor_xml.read_bytes())
        donor_root = next(node for node in donor_graph if node.get('cls') == 'Dialog')
        donor_image = donor_root.find("prop[@name='DialogDrawable']/object/prop[@name='Image']/value")
        assert donor_image.get('key').lower().endswith('d58f144d5b0fe27a')
        donor_scale = [float(x.get('value')) for x in donor_root.findall("prop[@name='DialogDrawable']/object/prop[@name='ScaleArea']/struct/prop")]
        assert donor_scale == [0.5] * 4, 'The selected donor has a real native nine-slice'
        native_buttons = []
        for node in donor_root.findall(".//object[@cls='Sims3IconButton']"):
            area = node.find("prop[@name='Area']")
            if area is not None:
                xy = [round(float(v), 2) for v in area.get('value').split(',')]
                if len(xy) == 4:
                    native_buttons.append(xy)
        assert [-69, -69, -13, -13] in native_buttons, '56-unit native confirm position'
        assert [-94, -56, -65, -27] in native_buttons, '29-unit native cancel position'
    # The Collection background is the fallback; the two-notch donor brings its own nine-slice configuration.
    assert 'UseDonor("HUDCollectionJournalDialog", 1,' in theme
    assert 'CopyFace(DialogFrame(journal), DialogFrame(donor));' in theme and 'CopyImages(DialogFrame(journal)' not in theme
    assert 'if (StyleTwoButtonFrame(journal))' in theme and 'StyleCancel(journal);' in theme
    assert '0x54D0A9831E5754E7UL' in theme and '0xB73E807A1250DC6FUL' in theme
    assert '0xBD8616686C83937BUL' in theme and 'TwoButtonExports = { 1, 1, 0x10000 }' in theme
    body = theme.split('private static bool StyleTwoButtonFrame(Dialog journal)')[1].split('private static void StyleCancel(')[0]
    assert 'CopyFace(target, source);' in body and 'CopyImages(target, source);' not in body
    assert 'journal.Invalidate();' in body
    assert 'target.ScaleArea = source.ScaleArea;' in theme.split('private static void CopyFace(')[1]
    assert 'NativeLayouts.Release(layout, false)' in body
    assert jl.COLLECTION_FRAME == '163462cf3324a2a8'
    assert jl.FRAME_MARGINS == (0.5, 0.5, 0.5, 0.5)
    assert jl.COLLECTION_FRAME in ET.tostring(root, encoding='unicode')
    native_refs = root_dir / 'references'
    required_donors = ['CollectionJournal/57560E847AC1F2F1.layout',
                       'TwoButtonNative/54D0A9831E5754E7.layout', 'TwoButtonNative/B73E807A1250DC6F.layout',
                       'TwoButtonNative/BD8616686C83937B.layout']
    if all((native_refs / donor).is_file() for donor in required_donors):
        check_r69(root_dir, window, theme)
        check_r70(root, root_dir, theme)
    else:
        print('SKIP: complete native frame donor XML is absent from this source drop')
    assert 'Undo' not in page + window + selection and 'UndoTier' not in selection, 'Undo is removed from the Perks page'
    assert 'enum CloseMode' in window and 'Confirm,' in window and 'Cancel,' in window and 'Abandon' in window
    assert 'Close(CloseMode.Confirm, keyActivation);' in window and 'Close(CloseMode.Cancel, keyActivation);' in window
    close = window.split('private void Close(CloseMode mode, bool keyboard)')[1].split('private void Dismiss()')[0]
    assert close.count('.Commit(') == 1 and 'mode == CloseMode.Confirm' in close.split('.Commit(')[0].split('if (')[-1]
    discard = selection.split('internal void DiscardStaged()')[1].split('// Buys every staged rank')[0]
    assert 'Array.Clear(staged, 0, staged.Length);' in discard and 'Commit' not in discard and 'acquired' not in discard, 'Confirmed perks stay as they were'
    assert 'internal int PowerPointsLeft { get { return powerPoints - PendingPowerCost; } }' in selection and 'tier < Tier(perk)' in selection.split('internal int PendingPowerCost')[1][:400], 'Unspent points follow the staged picks'
    discard_page = page.split('internal void Discard()')[1].split('// Buys what is staged')[0]
    assert 'selection.DiscardStaged();' in discard_page and 'RefreshPerkPoints' not in discard_page and 'Celebrate' not in discard_page and 'PurchaseEffect' not in discard_page, 'No purchase effect on cancel'
    assert window.count('perkPage.Commit(') == 2, 'The checkmark and tab confirmation use the shared commit'
    request = window.split('private void RequestPage(int index)')[1].split('private void ResolvePerkNavigation(')[0]
    resolve = window.split('private void ResolvePerkNavigation(')[1].split('private void DismissPerkConfirmation()')[0]
    prompt = cs_source.class_text(root_dir, 'PerkPurchaseDialog')
    assert 'index == page' in request and 'pending.PendingCount == 0' in request
    assert 'perkConfirmation != null' in request and 'ResolvePerkNavigation(prompt, index, pending, choice)' in request
    assert 'ActiveSelection != pending' in resolve and 'PerkPurchaseChoice.Dismissed' in resolve
    assert resolve.index('if (!perkPage.Commit(true))') < resolve.index('SelectPage(index);')
    assert 'perkPage.Discard();' in resolve and 'JournalBootstrap.RefreshPerkPoints();' in resolve
    assert 'PerkPurchaseDialog : TwoButtonDialog' in prompt and 'protected override void EndDialog(uint endID)' in prompt
    bridge = cs_source.class_text(root_dir, 'NativeUi')
    assert 'PauseModeField.SetValue(dialog, ModalDialog.PauseMode.NoPause);' in bridge
    assert prompt.index('NativeUi.DisableDialogPause(this);') < prompt.index('StartModal(false);')
    assert 'EndDialog(DismissID);' in prompt and 'base.EndDialog(endID);' in prompt
    assert 'window.TitleText = string.Empty;' in prompt and 'window.ShowTitle = false;' in prompt
    assert 'window.ShowTitleBar = false;' in prompt
    commit = selection.split('internal bool Commit()')[1]
    assert commit.index('if (record == null)') < commit.index('Array.Clear(staged, 0, staged.Length);')

    # 2. All three main tabs now have permanent matching 9-slice bodies and transparent button faces.
    assert ui_resources.TAB_HEIGHT == constant(window, 'TabHeight') and ui_resources.TAB_BODY_REACH == constant(window, 'TabReach')
    assert 8 <= ui_resources.TAB_BODY_REACH <= 11, 'Open-bottom tab shells reach exactly the same amount into the content panel'
    assert 'internal const ulong TabClearImage = 0x%016XUL;' % ui_resources.TAB_CLEAR_INSTANCE in constants.replace('0xCB3C845AFBAE189CUL', '0x%016XUL' % ui_resources.TAB_CLEAR_INSTANCE) or '%016X' % ui_resources.TAB_CLEAR_INSTANCE in constants.upper()
    assert ui_resources.TAB_CLEAR_INSTANCE == jl.fnv64(ui_resources.TAB_CLEAR_NAME) if hasattr(jl, 'fnv64') else True
    assert counts[jl.FIRST_TAB] == counts[jl.FIRST_TAB + 1] == counts[jl.FIRST_TAB + 2] == 1
    tab_order = [order.index(jl.FIRST_TAB + i) for i in range(3)]
    for i in range(3):
        body = control(root, jl.TAB_BODY + i)
        assert counts[jl.TAB_BODY + i] == 1, 'Each main tab owns its own tab body'
        assert image_slots(body, "prop[@name='FillDrawable']/object")[0] == f'{ui_resources.TAB_BODY_INSTANCE:016x}'
        assert body.find(".//prop[@name='ScaleType']").get('value') == '2'
        assert max(tab_order) < order.index(jl.TAB_BODY + i), 'Every tab body draws behind all main tab buttons'
    assert 'private readonly Window[] tabBodies = new Window[JournalPages.All.Length];' in window
    assert 'tabBodies[i] = Control<Window>(NativeLayouts.TabBody + (uint)i);' in window
    assert 'Place(body, tabLeft[i], TabTop, tabWidth[i], TabHeight + TabReach);' in window
    assert 'body.ShadeColor = i == page ? ActiveTabShade : InactiveTabShade;' in window
    assert 'body.Visible = show;' in window and 'tabs[i].Visible;' in window, 'Hidden page tabs have no stray shell'
    assert 'Place(tabs[i], left, TabTop, width, TabHeight);' in window, 'Tab button bounds never move on selection'
    assert 12 <= constant(window, 'TabLeft') - 25 <= 18 and 8 <= constant(window, 'TabGap') <= 10, 'Extra left clearance and an even gap between tabs'
    assert 'float left = TabLeft;' in window and 'left += width + TabGap;' in window and 'JournalPages.All[i].Title, TabLeft, TabTop, 137, TabHeight' in window, 'Tabs are placed as one group, one gap apart'
    faces = cs_source.class_text(root_dir, 'TabFaces')
    assert 'new TabFaces(tabs, StatusReaders.StatusIcon(NativeLayouts.TabClearImage))' in window
    assert 'face[Replaced[s]] = open != null ? open : (on ? selected : keep);' in faces, 'All main tab button faces are transparent'
    assert 'face[DrawableBase.ControlStates.kActive] = open ?? pressed[i];' in faces, 'No opaque rectangle flashes while pressing a tab'
    assert 'Fade' not in body.attrib.get('cls', '') and 'Slide' not in window.split('PlaceTabBodies')[1][:900], 'No sliding animation'
    size, px = generated_rgba(ui_resources.tab_body_png())
    assert size == (ui_resources.TAB_BODY_WIDTH, ui_resources.TAB_BODY_HEIGHT)
    middle = ui_resources.TAB_BODY_WIDTH // 2
    assert px[middle, 0][3] > 200 and px[middle, 0][:3] != (255, 255, 255), 'An outline along the top'
    assert px[middle, ui_resources.TAB_BODY_HEIGHT - 1][3] < 40 or px[middle, ui_resources.TAB_BODY_HEIGHT - 1][:3] != ui_resources.TAB_BODY_OUTLINE, 'No outline along the bottom'
    assert all(abs(a - b) > 40 for a, b in zip(px[middle, ui_resources.TAB_BODY_HEIGHT - 1][:3], ui_resources.TAB_BODY_OUTLINE)) or px[middle, ui_resources.TAB_BODY_HEIGHT - 1][3] < 40
    assert px[0, 0][3] < 160, 'A rounded upper corner'
    assert px[0, ui_resources.TAB_HEIGHT // 2][3] > 200, 'A side outline'
    _, clear = generated_rgba(ui_resources.tab_clear_png())
    assert max(pixel[3] for pixel in clear.values()) == 0, 'The tab face is fully clear'
    for name in ('tab_body', 'TAB_BODY_INSTANCE'):
        assert name in (root_dir/'tools'/'journal_layouts.py').read_text()
    assert ('SupernaturalExpanded.UI.TabBody' in (root_dir/'tools'/'ui_resources.py').read_text())

    # 4. Fairy and other species without a progression system: no fabricated level, no invented rank.
    read = status.split('internal static bool ReadSkill(')[1].split('// The game\'s own icon for the skill')[0]
    assert read.index('SkillManager.GetStaticSkill(name)') < read.index('field.Set(label, JournalText.Format("Arcana.Status.Level", 0)'), 'Level 0 is only for a skill the game defines'
    check_text(read, 'Arcana.Status.Level', 'Level {0}')
    assert 'template == null || template.MaxSkillLevel <= 0' in read and 'level < 0 || max <= 0' in read
    assert 'if (level > max)' in read and 'if (level < max)' in read
    assert 'float lower = level == 0 ? 0f : thresholds[level - 1];' in read
    assert 'float upper = thresholds[level];' in read
    assert 'fraction = (skill.SkillPoints - lower) / (upper - lower);' in read, 'Progress must be measured inside the current level, not against cumulative XP'
    assert 'upper <= lower' in read and 'float.IsInfinity(skill.SkillPoints)' in read
    assert read.count('return false;') == 5, 'No reading for an undefined skill, an absent Sim or unusable data'
    assert 'float.IsNaN(fraction)' in status.split('internal void Set(')[1][:400]
    assert 'StatusReaders.ReadMotive(source, CommodityKind.AuraPower, JournalText.Get("Arcana.Status.Label.FaericPower"), field);' in status, 'Faeric Power is still read'
    check_text(status, 'Arcana.Status.Label.FaericPower', 'Faeric Power')
    non_witch = data.split('private sealed class WitchOverview')[0]
    assert 'Novice' not in rank + non_witch, 'No rank is invented for a species without a ladder'
    assert 'internal static bool HasLadder(OccultTypes types)' in rank and 'return OccultAssetData.Primary(types) == OccultTypes.Witch;' in rank
    assert 'return "";' in rank.split('internal static string Rank(')[1].split('internal static string For(')[0], 'Unknown or unsupported progression gives no rank'
    assert 'string lifeState = LifeStateNames.For(primary);' in rank
    assert 'return rank.Length == 0 ? lifeState : JournalText.Format("Arcana.Common.WithQualifier", lifeState, rank);' in rank
    check_text(rank, 'Arcana.Common.WithQualifier', '{0} ({1})')
    assert 'data.RankTitle = JournalText.Get("Arcana.Journal.Overview.Rank.Unranked");' in data
    assert 'data.RankTitle = JournalText.Get("Arcana.Journal.Overview.Rank.Unavailable");' in data
    check_text(data, 'Arcana.Journal.Overview.Rank.Unranked', 'Rank: Unranked')
    check_text(data, 'Arcana.Journal.Overview.Rank.Unavailable', 'Rank: Unavailable')
    assert data.index('data.RankLevel = 1;') < data.index('"Arcana.Journal.Overview.Rank.Unranked"'), 'Only a real rank sets a numeral'
    check_text(data, 'Arcana.Journal.Overview.Rank.Unimplemented', 'Rank progression for this life state has not been added yet.')
    tree = cs_source.file_text(root_dir, 'OccultTree')
    assert 'private int ClampRank(int rank)' in tree
    assert 'rank >= RankNames.Length ? RankNames.Length - 1 : rank' in tree
    assert 'int index = ClampRank(rank);' in tree and 'RankNames[index]' in tree and 'rankTitles[index]' in tree
    assert 'rank = ClampRank(rank);' in tree and 'RankNames[rank];' in tree, 'A rank outside the table cannot throw'
    assert 'WitchProgression' not in status.split('class FairyStatus')[1].split('class WerewolfStatus')[0], 'Fairies never read Witch progression'

    # 8. Knowledge rows: sized from the panel (a mirror of OverviewList.Fit), the other lists untouched, separators across the full width.
    share = constant(lst, 'FillShare')
    assert 0.85 <= share <= 0.90
    default_row, max_row = constant(lst, 'RowHeight'), constant(lst, 'MaxRowHeight')
    usable = jl.OV_LIST_ROWS[2] * default_row
    capacity = jl.OV_LIST_ROWS[2]
    def fit(count):
        row = max(default_row, min(max_row, math.floor(usable * share / count)))
        return row, max(1, min(capacity, int(math.floor(usable / row + 0.001))))
    for count in (4, 5, 6):
        row, slots = fit(count)
        assert 0.85 <= count * row / usable <= 0.90, f'{count} rows fill {count * row / usable:.2f} of the panel'
        assert count <= slots
    row, slots = fit(4)
    assert row > default_row and math.floor(row * constant(lst, 'IconShare')) <= constant(lst, 'MaxIconSize') and math.floor(row * constant(lst, 'IconShare')) > constant(lst, 'IconSize'), 'Four rows get larger rows and icons'
    assert fit(1)[0] == max_row and fit(8) == (default_row, capacity) and fit(12) == (default_row, capacity), 'Other counts stay within limits and scroll'
    assert 'if (fill && rows.Count > 0)' in lst and 'Math.Floor(listHeight * FillShare / rows.Count)' in lst, 'Computed from the available height and the number of rows'
    assert 'nextTop = (float)Math.Floor((listHeight - rows.Count * nextRow) / 2f);' in lst and 'float y = listTop + rowsTop + i * rowHeight;' in lst, 'The rows are centred in the panel, the rest split evenly above and below'
    assert 'float ruleInset = fill ? 0f : Padding;' in lst and 'journal.Place(rules[i], listLeft + ruleInset, y + rowHeight - 1f, rowWidth - 2f * ruleInset, 1f);' in lst
    assert 'journal.Place(icons[i], listLeft + Padding, y + (float)Math.Floor((rowHeight - iconSize) / 2f), iconSize, iconSize);' in lst, 'Icons stay in the left column, vertically centred on a whole unit'
    assert 'float textLeft = listLeft + Padding + iconSize + IconGap;' in lst
    assert 'rules[i].Visible = show && i < lastRule;' in lst and 'int lastRule = fill ? Math.Min(slots, count - first) - 1 : slots - 1;' in lst and 'bool show = visible && i < slots && index < count;' in lst and 'return Math.Max(0, rows.Count - slots);' in lst, 'Rows beyond the panel scroll'
    assert 'lists[list].Layout(x + 1f, y + HeaderHeight, width - 2f, height - HeaderHeight - BottomEdge);' in overview
    for index in range(3):
        base = jl.OV_LIST_FIRST + index * jl.OV_LIST_STRIDE + 1
        assert control(root, base).find("prop[@name='VerticalAlign']").get('value') == '1' and control(root, base + 2).find("prop[@name='VerticalAlign']").get('value') == '1', 'Names and counts are centred vertically in their row'
    assert overview.count('0f, 110f, false, true);') == 1 and overview.count('56f, 96f, false, false);') == 1 and overview.count('0f, 64f, true, false);') == 1, 'Only Knowledge fills its panel, only Attributes is compact'
    print('PASS: r62: Cancel X has a native circular checkmark-derived face (separate original X glyph), and only Confirm commits; all three main tabs have permanently extended shells with tint-only selection; Fairy progression safe; Knowledge rows fill %.0f%% of their panel for four categories' % (400 * fit(4)[0] / usable))


def control(root, cid):
    for node in root.iter('object'):
        prop = node.find("prop[@name='ControlID']")
        if prop is not None and int(prop.get('value'), 16) == cid:
            return node
    raise AssertionError(f'Missing {cid:08X}')


def layout_font(root, cid):
    return control(root, cid).find("prop[@name='TextFont']").get('value')


def child_order(root):
    ids = []
    for node in root.find('object').find("prop[@name='Children']").findall('object'):
        prop = node.find("prop[@name='ControlID']")
        ids.append(int(prop.get('value'), 16) if prop is not None else -1)
    return ids


def image_slots(node, drawable_path):
    values = node.find(drawable_path).findall("prop[@name='Image']/value")
    return [v.get('key').split(':')[-1] for v in values]


def constant_int(source, name):
    match = re.search(r'const int ' + name + r' = (\d+);', source)
    assert match, name
    return int(match.group(1))


GEOMETRY_SOURCE = cs_source.file_text(Path(__file__).resolve().parents[1], 'WindowGeometry')


def constant(source, name):
    match = re.search(r'const (?:float|int) ' + name + r' = ([^;]+);', source)
    if not match:
        match = re.search(r'const (?:float|int) ' + name + r' = ([^;]+);', GEOMETRY_SOURCE)
        source = GEOMETRY_SOURCE
    assert match, name
    expression = re.sub(r'\b[A-Z]\w*\.', '', match.group(1))
    for other in set(re.findall(r'\b[A-Z]\w+', expression)):
        expression = re.sub(r'\b' + other + r'\b', lambda m, o=other: repr(constant(source, o)), expression)
    return eval(re.sub(r'(\d)f\b', r'\1', expression))


def check_phase_c(root, layout, cs, root_dir, constants):
    # Dialog frame: fixed margins, never the stock centre slice.
    top = root.find('object')
    frame = top.find("prop[@name='DialogDrawable']/object")
    area = [float(p.get('value')) for p in frame.findall("prop[@name='ScaleArea']/struct/prop")]
    assert area == list(FRAME_MARGINS) == [0.5] * 4, 'The generated frame uses the native 0.5 slicing'
    assert image_slots(top, "prop[@name='DialogDrawable']/object")[0] == COLLECTION_FRAME
    theme = cs_source.class_text(root_dir, 'JournalTheme')
    assert 'CopyFace(DialogFrame(journal), DialogFrame(donor))' in theme and 'CopyFace(target, source);' in theme, 'Both frames take their artwork and native slicing from their donor together'
    assert 'CopyImages(DialogFrame(journal)' not in theme
    # Native artwork in the Grimoire.
    for key in (HEADER_CELL, LIST_BODY, SCROLL_TRACK, THUMB_KEY, SCROLL_UP_GLOW, SCROLL_DOWN_GLOW, SCROLL_THUMB_GLOW):
        assert layout.count(key.encode()) >= 1, f'Stock sprite {key} missing'
    assert layout.count(HEADER_CELL.encode()) == 3 + 3 and layout.count(LIST_BODY.encode()) == 2 + 5, 'Grimoire plus Overview panels'
    for cid, images in ((GRIMOIRE_SCROLL_UP, SCROLL_UP_ARROW), (GRIMOIRE_SCROLL_DOWN, SCROLL_DOWN_ARROW)):
        slots = image_slots(control(root, cid), "prop[@name='ButtonDrawable']/object")
        assert slots[0] == images[0] and slots[3] == images[1] and slots[1] == slots[2] == '', 'Arrow slots'
    for i in range(3):
        cell = control(root, GRIMOIRE_FIRST_COLUMN_BUTTON + i)
        assert image_slots(cell, "prop[@name='ButtonDrawable']/object")[0] == HEADER_CELL
        cut = [float(p.get('value')) for p in cell.findall(".//prop[@name='ScaleArea']/struct/prop")]
        assert cut == [0.45, 0.333333343, 0.45, 0.333333343], 'Header cell slice'
        assert control(root, GRIMOIRE_FIRST_SORT_ARROW + i).find(".//prop[@name='AlignmentHorizontal']").get('value') == '2'
    # Caption centering is a two-part contract: native Text.HAlign must be center,
    # AND the C# placement must span the whole column (not the area left of its sort arrow).
    for cid in (GRIMOIRE_LIST_TITLE, GRIMOIRE_TIER_HEADER, GRIMOIRE_COST_HEADER):
        assert control(root, cid).find("prop[@name='HorizontalAlign']").get('value') == '1', 'Header caption is not centered'
    assert 'journal.Place(headers[i], starts[i], headY, cellWidth, HeaderHeight);' in cs, 'Header captions must occupy the complete column width'
    assert 'journal.Place(sortArrows[i], starts[i], headY, cellWidth - 3f, HeaderHeight);' in cs, 'Sort arrow placement changed'
    # Panel outlines draw in front of their bodies, rows and the header in front of both.
    order = child_order(root)
    assert order.index(GRIMOIRE_LIST_PANEL) < order.index(GRIMOIRE_LIST_BODY)
    assert order.index(GRIMOIRE_DESC_PANEL) < order.index(GRIMOIRE_DESC_BODY)
    assert order.index(GRIMOIRE_FIRST_ROW) < order.index(GRIMOIRE_LIST_PANEL)
    assert order.index(GRIMOIRE_FIRST_COLUMN_BUTTON) < order.index(GRIMOIRE_LIST_PANEL)
    assert order.index(GRIMOIRE_FIRST_SEPARATOR) < order.index(GRIMOIRE_FIRST_ROW), 'Dividers must draw over the rows'
    for i in range(2):
        assert order.index(GRIMOIRE_FIRST_TABLE_CORNER + i) < order.index(GRIMOIRE_FIRST_TABLE_EDGE + i), 'Corners must draw over the edge lines'
        assert order.index(GRIMOIRE_FIRST_TABLE_CORNER + i) < order.index(GRIMOIRE_FIRST_SEPARATOR)
        assert order.index(GRIMOIRE_FIRST_TABLE_EDGE + i) < order.index(GRIMOIRE_FIRST_COLUMN_BUTTON), 'Edge lines must draw over the header'
        assert order.index(GRIMOIRE_FIRST_TABLE_EDGE + i) < order.index(GRIMOIRE_FIRST_ROW)
    # Column dividers are the stock Lifetime Rewards divider (same artwork and fill); the header edge lines continue the outline.
    assert layout.count(DIVIDER.encode()) == 2, 'Only the two Grimoire column dividers use the stock divider artwork; Life State has none'
    for i in range(2):
        divider = control(root, GRIMOIRE_FIRST_SEPARATOR + i)
        assert divider.find("prop[@name='FillColor']").get('value') == DIVIDER_FILL
        assert image_slots(divider, "prop[@name='FillDrawable']/object")[0] == DIVIDER
        edge = control(root, GRIMOIRE_FIRST_TABLE_EDGE + i)
        assert edge.find("prop[@name='FillColor']").get('value') == TABLE_EDGE
        assert edge.find("prop[@name='FillDrawable']") is None
    assert order.index(GRIMOIRE_SCROLL_THUMB) < order.index(GRIMOIRE_SCROLL_TRACK)
    assert order.index(GRIMOIRE_DESC_TEXT) < order.index(GRIMOIRE_DESC_PANEL)
    fills = [control(root, GRIMOIRE_FIRST_ROW + 5 * i).find("prop[@name='FillColor']").get('value') for i in range(GRIMOIRE_ROWS)]
    assert fills == ['0x00000000' if i % 2 == 0 else ROW_ALTERNATE for i in range(GRIMOIRE_ROWS)], 'Row stripes'
    # The Overview, Grimoire and Perks panels are the stock pale blue surround; the white list body stays Grimoire-only.
    perks_panel = WITCH.id(6, 11)
    for cid in (CONTENT, perks_panel):
        face = control(root, cid).find("prop[@name='FillDrawable']")
        assert SURROUND in ET.tostring(face, encoding='unicode'), 'Inner panel must use the stock pale blue surround'
    assert layout.count(SURROUND.encode()) == 2, 'Only the Overview/Grimoire and Perks panels use the surround; the rank emblem well is gone'
    # The header has no highlight behind its text: the pale streak that glowed behind the rank line is gone.
    assert 'IDENTITY_PANEL' not in (root_dir/'tools'/'journal_layouts.py').read_text() and not (root_dir/'tools'/'header_streak.py').exists()
    assert 'IdentityPanel' not in (root_dir/'src'/'Core'/'NativeLayouts.cs').read_text() and 'treak' not in cs_source.class_text(root_dir, 'JournalWindow') and 'identityPanel' not in cs_source.class_text(root_dir, 'JournalWindow')
    from dbpf import fnv64
    assert fnv64('SupernaturalExpanded.UI.HeaderStreak') not in {instance for instance, _ in resources()}, 'The streak image is no longer packaged'
    # One list outline for the list and the description, both with the white list body behind them.
    assert layout.count(LIST_OUTLINE.encode()) == 2 + 5, 'The Grimoire list and description and the five Overview panels share the stock list outline'
    for cid in (GRIMOIRE_LIST_PANEL, GRIMOIRE_DESC_PANEL):
        assert LIST_OUTLINE in ET.tostring(control(root, cid).find("prop[@name='FillDrawable']"), encoding='unicode')
    # Rounded header corners: two generated images, one mirrored, sized as the C# constant, hiding the square corner.
    corner_size = constant(cs, 'CornerSize')
    assert corner_size == ui_resources.TABLE_CORNER_SIZE, 'Corner image and window size must match'
    packaged = dict(resources())
    for instance, data in ((ui_resources.TABLE_CORNER_INSTANCE_LEFT, ui_resources.table_corner_left_png()), (ui_resources.TABLE_CORNER_INSTANCE_RIGHT, ui_resources.table_corner_right_png())):
        assert packaged[instance] == data and data.startswith(b'\x89PNG'), 'Corner image must be packaged'
    for i, instance in enumerate((ui_resources.TABLE_CORNER_INSTANCE_LEFT, ui_resources.TABLE_CORNER_INSTANCE_RIGHT)):
        assert image_slots(control(root, GRIMOIRE_FIRST_TABLE_CORNER + i), "prop[@name='FillDrawable']/object")[0] == f'{instance:016x}'
    assert ui_resources.table_corner_pixel(0, 0)[3] == 255 and ui_resources.table_corner_pixel(0, 0)[:3] == ui_resources.TABLE_CORNER_SURROUND, 'The square corner must be covered'
    assert ui_resources.table_corner_pixel(ui_resources.TABLE_CORNER_SIZE - 1, ui_resources.TABLE_CORNER_SIZE - 1)[3] == 0, 'The header must show through the corner'
    assert 'journal.Place(corners[0], headerX, headY, CornerSize, CornerSize);' in cs
    assert 'journal.Place(corners[1], headerX + tableWidth - CornerSize, headY, CornerSize, CornerSize);' in cs
    # Page tabs: navy caption in every state, and a pressed face in every checked slot (replaced at run time, see below).
    for i in range(3):
        tab = control(root, FIRST_TAB + i)
        assert [v.text for v in tab.findall("prop[@name='CaptionColors']/value")] == [NAVY] * 8, 'Tab captions must stay navy when selected'
        assert image_slots(tab, "prop[@name='ButtonDrawable']/object")[3:] == [DIALOG_BUTTON[3]] * 5, 'A selected tab must never use the disabled face'
    # The header meters are the stock HUD cylindrical bar.
    check_header_bars(root, root_dir, order)
    assert FOOTER_FONT == '0x062eae0d' and layout.count(FOOTER_FONT.encode()) == 3 + 5
    assert order.index(perks_panel) < order.index(CONTENT)
    window_src = cs_source.class_text(root_dir, 'JournalWindow')
    assert 'ContentBody' not in window_src
    theme_src = cs_source.class_text(root_dir, 'JournalTheme')
    assert 'NativeLayouts.Content,' not in theme_src and 'PageSlot.PowersPanel' not in theme_src, 'The surround panels keep their own stock face'
    # Final polish: headings, header alignment, multi-line text, scrollbar, description, tab faces.
    assert 'PowersTitle' not in cs_source.file_text(root_dir, 'PowerPage')
    assert not {0x53E00007, 0x53E00008}.intersection(order), 'The compact text Overview has been removed'
    node = control(root, GRIMOIRE_DESC_TEXT)
    assert node.find("prop[@name='WordWrap']").get('value') == '4' and node.find("prop[@name='Overflow']").get('value') == '1', 'Multi-line text must wrap and clip, not ellipsize'
    assert control(root, GRIMOIRE_DESC_NAME).find("prop[@name='Overflow']").get('value') == '2'
    assert 'scrollTrack.Visible = scrollable;' in cs and 'upArrow.Visible = scrollable;' in cs and 'downArrow.Visible = scrollable;' in cs, 'The track and arrows appear only when the list scrolls'
    assert 'scrollThumb.Visible = scrollable;' in cs, 'The thumb appears only when the list scrolls'
    assert 'scrollTrack.Visible = visible;' not in cs and 'upArrow.Visible = visible;' not in cs, 'An empty list shows no scrollbar'
    assert 'upArrow.Enabled = scrollable &&' in cs and 'downArrow.Enabled = scrollable &&' in cs
    assert 'descriptionBody.Visible = show;' in cs and 'descriptionBody.Visible = entry' not in cs, 'The description body matches the list and is always shown'
    assert 'PlaceSeparators' not in cs and 'hasEntries ? ' not in cs, 'Dividers must not depend on the row count'
    assert 'journal.Place(separators[i], starts[i + 1] - 1f, headY, 1f, PaneHeight);' in cs, 'Dividers span the whole table'
    assert 'journal.Place(edges[0], headerX, headY, 1f, HeaderHeight);' in cs
    assert 'journal.Place(edges[1], headerX + tableWidth - 1f, headY, 1f, HeaderHeight);' in cs
    assert 'float outlineLeft = margin - OutlineInset;' in cs and 'float outlineWidth = tableWidth + 2f * OutlineInset;' in cs
    assert 'journal.Place(listPanel, outlineLeft, listTop, outlineWidth, outlineHeight);' in cs
    assert 'journal.Place(descriptionPanel, outlineLeft, descriptionTop, outlineWidth, DescriptionHeight);' in cs, 'The description is exactly as wide as the table'
    assert 'tableWidth - 28f' in cs and 'paneWidth - 28f' not in cs
    assert 'nameWidth - 10f, 40f);' in cs and 'usableWidth - 10f' not in cs, 'The empty message stays in the Name column'
    assert 'float headerX = margin;' in cs and 'float tableWidth = paneWidth - ScrollWidth - ScrollGap;' in cs, 'Header cells span exactly the table'
    assert 'scrollbarLeft = margin + paneWidth - ScrollWidth;' in cs, 'The scrollbar sits outside the table, as in Lifetime Rewards'
    assert 'cellsRight' not in cs
    assert 'float listTop = headY + HeaderHeight - 1f;' in cs and 'float headY = PaneTop;' in cs, 'The header band sits above the list outline, as in the stock lists'
    assert 'NativeLayouts.GrimoireListPanel' not in theme_src.split('StyleInnerPanels(Dialog journal, WindowBase donor)')[1].split('private static void StyleClose')[0], 'The list outline is not replaced by a donor face'
    faces_src = cs_source.class_text(root_dir, 'TabFaces')
    assert 'new TabFaces(tabs, StatusReaders.StatusIcon(NativeLayouts.TabClearImage))' in window_src and 'new TabFaces(grimoirePage.Tabs)' in window_src, 'Page tabs draw a clear face over their own body, categories the pressed face'
    assert 'plainFace ? original[i][0] : pressed[i]' in faces_src and 'on ? SelectedTint : Untinted' in faces_src, 'Selected page tab: plain face and tint, no pressed face'
    assert 'pageFaces.Show(page)' in window_src and 'tabFaces.Show(index)' in cs
    geo = cs_source.file_text(root_dir, 'WindowGeometry')
    header_top, row_center, row_pitch = constant(geo, 'PanelTop'), constant(geo, 'FirstRowCenter'), constant(geo, 'RowPitch')
    status_center = header_top + row_center + row_pitch / 2.0
    identity_center = constant(window_src, 'IdentityTop') + constant(window_src, 'IdentityLineSpacing') + constant(window_src, 'IdentityLineHeight') / 2.0
    assert abs(status_center - identity_center) < 0.01, 'The status panel must be centred on the identity block and portrait'
    assert 'IdentityPlate' not in window_src, 'The rectangular plate constants are gone'
    grid_top = constant(geo, 'GridTop')
    assert grid_top == 178.0, 'The tree moves up by exactly 12 units'
    # Description plumbing: optional text on the entry, fixed-height panel, derived from the selection only.
    assert 'internal readonly string Description;' in cs and 'description ?? String.Empty' in cs
    assert 'RefreshDescription();' in cs and 'FindId(sorted[category], selectedIds[category])' in cs
    assert 'ShortenDescription' in cs and 'MaxDescriptionLength' in cs
    body = cs[cs.index('private void RefreshDescription()'):cs.index('private static string ShortenDescription')]
    assert 'Place(' not in body and 'Area' not in body, 'The description must not change geometry'
    window = cs_source.class_text(root_dir, 'JournalWindow')
    assert 'GrimoireHeight' not in window and 'ContentBottom' not in cs.replace('WindowGeometry.ContentBottom', ''), 'The Grimoire has no window height of its own'
    # Text colour must survive a skin's text donor.
    for name in ('GrimoireDescriptionName', 'GrimoireDescriptionText'):
        assert f'NativeLayouts.{name}' in theme, name
    # Perks: one centre line for the rank bar, notches and markers, and the raised help icon.
    page = cs_source.file_text(root_dir, 'PowerPage')
    geometry = cs_source.file_text(root_dir, 'WindowGeometry')
    assert 'RankLeft' not in page and 'RankLeft' not in geometry, 'The rank bar must not use a fixed left'
    assert page.count('barCenter') >= 4 and 'PowerLayout.RankBarCenter(leftmost)' in page
    assert 'HelpRaise' in page and 'FooterIconRaise' in page and 'HelpRaise = 6.5f' in geometry
    panel_left, lanes_left = constant(geometry, 'PowersPanelLeft'), constant(geometry, 'LanesLeft')
    assert lanes_left > panel_left + 80, 'The rank bar needs room before the first perk column'
    # Geometry, recomputed from the C# constants.
    pane_top, pane_height = constant(cs, 'PaneTop'), constant(cs, 'PaneHeight')
    desc_height = constant(cs, 'DescriptionHeight')
    header, row = constant(cs, 'HeaderHeight'), constant(cs, 'RowHeight')
    scroll, arrow, gutter = constant(cs, 'ScrollWidth'), constant(cs, 'ScrollArrowHeight'), constant(cs, 'ArrowGutter')
    rows = int(re.search(r'const int VisibleRows = (\d+);', cs).group(1))
    assert rows == GRIMOIRE_ROWS, 'Row slots in the layout and the C# pool must match'
    assert pane_top + header + rows * row <= pane_top + pane_height - 2, 'Rows overflow the list pane'
    # Perk Points footer: the group scales together (about 1.15) and stays inside the footer band.
    disc, icon_size, cap = constant(geometry, 'HelpDiscSize'), constant(geometry, 'FooterIconSize'), constant(geometry, 'CaptionWidth')
    assert abs(disc / 27.0 - icon_size / 19.0) < 0.02 and abs(cap / 96.0 - disc / 27.0) < 0.03, 'The footer group must scale proportionally'
    assert disc + 2 * constant(geometry, 'HelpRaise') <= constant(geometry, 'FooterHeight') and icon_size <= constant(geometry, 'FooterHeight')
    assert 2 * arrow < rows * row
    inset, extra = constant(cs, 'OutlineInset'), constant(cs, 'OutlineBottomExtra')
    # The outline's visible lines sit 3 units inside its area (4 at the bottom), measured from the in-game list.
    assert inset == 3 and extra == 2, 'Outline widened onto the header edges'
    assert pane_top + header + rows * row <= pane_top + pane_height + extra - 4, 'The last row must sit above the bottom outline line'
    assert abs(pane_top + pane_height + constant(cs, 'DescriptionGap') + desc_height + constant(cs, 'BottomPad') - constant(geometry, 'ContentBottom')) < 0.01, 'The description panel ends at the shared content region'
    assert 100 <= desc_height <= 140, 'A balanced description panel'
    assert rows >= 12 and row == 30, 'The list shows twelve rows of 30 units'
    assert constant(cs, 'TabTop') + 39 < pane_top, 'Category buttons must clear the list'
    assert desc_height >= 64 + 3 * 17, 'The description box must hold three lines under the name and the Tier / Energy Cost line'
    for width in (896.0,):
        pane = width
        usable = pane - scroll - constant(cs, 'ScrollGap')
        name, tier, cost = usable * 0.57, usable * 0.20, usable - usable * 0.77
        assert name > cost > tier > 2 * gutter, f'Column widths at {width}'
    print('PASS: Phase C geometry, draw order, native sprite slots, help icon and description wiring')


def effect_values(node):
    """Reads each native effect (WinProc) of a window as a dictionary of its properties."""
    procs = []
    for proc in node.findall("prop[@name='WinProcs']/object"):
        values = {p.get('name'): p.get('value') for p in proc.findall('prop')}
        values['class'] = proc.get('cls')
        procs.append(values)
    return procs


def glow_of(node):
    drawable = node.find("prop[@name='FillDrawable']/object")
    return {p.get('name'): p.get('value') if p.get('key') is None else p.get('key').split(':')[-1]
            for p in drawable.findall('prop') if p.get('name', '').startswith('Glow')}


def check_namespaces(root_dir):
    """Cheap guards for compile errors the C# compiler would otherwise be the first to report."""
    owners = {'OccultTypes': 'Sims3.UI.Hud', 'IHudModel': 'Sims3.UI.Hud', 'Audio': 'Sims3.SimIFace', 'ResetException': 'Sims3.SimIFace',
              'IconZoop': 'Sims3.UI.Hud', 'UIImage': 'Sims3.UI', 'ISimClockSpeedModel': 'Sims3.UI', 'ImageDrawable': 'Sims3.UI',
              'Vector2': 'Sims3.SimIFace'}
    for path in sorted((root_dir/'src').rglob('*.cs')):
        text = path.read_text(encoding='utf-8-sig')
        code = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', re.sub(r'//[^\n]*', '', text))
        imported = set(re.findall(r'^using ([\w.]+);', text, re.M))
        for name, namespace in owners.items():
            if re.search(r'(?<![.\w])' + name + r'\b', code):
                assert namespace in imported, f'{path.name} uses {name} without "using {namespace};"'
        if {'System', 'Sims3.SimIFace'} <= imported:
            assert not re.search(r'(?<![.\w])Environment\.', code), \
                f'{path.name}: Environment is ambiguous between System and Sims3.SimIFace; write System.Environment'


def check_animation_drawable(node, instance):
    face = node.find("prop[@name='FillDrawable']/object")
    assert face.get('cls') == 'ImageDrawable'
    assert face.find("prop[@name='Image']").get('key') == f'2f7d0004:00000000:{instance:016x}'
    assert not effect_values(node), 'A native fade must not compete with the animation clock'


def check_perk_feedback(root, root_dir, constants):
    """Check the animation layout, resource bindings, input flags, draw order and lifecycle wiring."""
    from package_journal import content_resources
    from trees import IMAGE

    def flags(node):
        return int(node.find("prop[@name='WindowFlags']").get('value'), 16)

    def control_id(node):
        return int(node.find("prop[@name='ControlID']").get('value'), 16)

    assert re.search(r'HudButton = "\w+_r16"', constants)
    button = ET.fromstring(dict(layouts())[HUD_BUTTON]).find('object')
    glyph, ring = button.findall("prop[@name='Children']/object")
    assert [control_id(glyph), control_id(ring)] == [ENTRY_GLYPH, PERK_PULSE]
    assert button.get('id') == '0x00000001'
    assert flags(ring) & 0x11 == 0x10, 'The ring starts hidden and is click-through'
    check_animation_drawable(ring, ui_resources.HALO_INSTANCE)
    assert ui_resources.halo_pixel(ui_resources.HALO_WIDTH // 2, ui_resources.HALO_HEIGHT // 2)[3] == 0
    assert max(ui_resources.halo_pixel(x, ui_resources.HALO_HEIGHT // 2)[3] for x in range(ui_resources.HALO_WIDTH)) > 170

    page_id = lambda slot: WITCH.id(6, slot)
    overlay = control(root, page_id(119))
    clip = control(root, page_id(40))
    tip_clip = control(root, page_id(64))
    for node in (overlay, clip, tip_clip):
        assert flags(node) & 0x411 == 0x410, 'Effect parents start hidden, clip and ignore the mouse'
    for parent, first, count, instance in ((overlay, 120, SPARK_PARTICLES, ui_resources.STAR_INSTANCE),
            (clip, 41, RANK_GLITTER_TILES, ui_resources.GLITTER_BODY_INSTANCE),
            (tip_clip, 65, 1, ui_resources.GLITTER_TIP_INSTANCE)):
        children = parent.findall("prop[@name='Children']/object")
        assert len(children) == count
        for i, node in enumerate(children):
            assert control_id(node) == page_id(first + i) and flags(node) & 0x10
            check_animation_drawable(node, instance)
    order = [control_id(n) for n in root.find('object').findall("prop[@name='Children']/object")]
    assert order.index(page_id(119)) < order.index(WITCH.id(0, 0)), 'Sparkler is above perk tiles'
    assert order.index(page_id(64)) < order.index(WITCH.id(5, 0)), 'Tip is visible in front of rank markers'
    assert order.index(WITCH.id(5, 0)) < order.index(page_id(40)) < order.index(page_id(1)) < order.index(page_id(0))
    assert page_id(12) not in order and page_id(13) not in order
    # Each perk's edge window is above its tile and its plates, takes no clicks, and draws the shared white edge image.
    for i in range(len(WITCH.catalog())):
        assert order.index(WITCH.id(9, i)) < order.index(WITCH.id(0, i)), 'Outline over the tile'
        assert all(order.index(WITCH.id(9, i)) < order.index(WITCH.id(kind, i)) for kind in (1, 2, 3, 4, 7, 8)), 'Outline over the plates, so no plate cuts it'
        edge = control(root, WITCH.id(9, i))
        assert flags(edge) == 0x2012, 'The edge takes no clicks'
        check_animation_drawable(edge, ui_resources.TILE_EDGE_INSTANCE)

    images = {key.instance: png for key, png in content_resources() if key.type == IMAGE}
    for instance in (ui_resources.HALO_INSTANCE, ui_resources.GLITTER_BODY_INSTANCE, ui_resources.GLITTER_TIP_INSTANCE,
                     *(ui_resources.STAR_INSTANCE + i for i in range(ui_resources.STAR_FRAMES))):
        assert images[instance].startswith(b'\x89PNG\r\n\x1a\n')

    src = root_dir/'src'
    feedback = cs_source.class_text(root_dir, 'PerkFeedback')
    sounds = cs_source.class_text(root_dir, 'JournalSounds')
    page = cs_source.file_text(root_dir, 'PowerPage')
    entry = cs_source.class_text(root_dir, 'SimologyEntry')
    window = cs_source.class_text(root_dir, 'JournalWindow')
    pulse = cs_source.class_text(root_dir, 'PerkPulse')
    burst = cs_source.class_text(root_dir, 'SparkleBurst')
    glitter = cs_source.class_text(root_dir, 'RankGlitter')
    clock = cs_source.class_text(root_dir, 'UiAnimationClock')
    assert SPARK_PARTICLES == constant(burst, 'BurstCount') * constant(burst, 'ParticlesPerBurst')
    assert constant(burst, 'SpinFrames') == ui_resources.STAR_FRAMES and f'{ui_resources.STAR_INSTANCE:X}UL' in burst
    assert RANK_GLITTER_TILES == constant(glitter, 'TileCount')
    shimmer = cs_source.class_text(root_dir, 'BarSparkles')
    header = cs_source.class_text(root_dir, 'StatusHeader')
    assert all('.Tick +=' not in effect and 'RunLater' not in effect for effect in (burst, glitter, pulse, shimmer))
    for effect in (burst, glitter, pulse, shimmer):
        assert effect.count('new UiAnimation(') == 1, 'Every effect runs on the one shared animation clock'
    # The sparkler is the protected baseline: its particle maths, tints and timings are compared with the working build.
    for needle in ('ParticlesPerBurst = 28', 'BurstCount = 3', 'SpinFrames = 12', '0xFFCE4D, 0xFFE04F, 0xFFEE91, 0xFFF8C4, 0xFFFFF5',
                   'Range(27f, 65f)', 'Range(6f, 8f) : i % 3 == 0 ? Range(4f, 5f) : Range(2.5f, 3.8f)', 'random.Next(900, 1190)',
                   'random.Next(0, 85)', 't * (1.72f - 0.72f * t)', '(1f - t) / 0.53f', '0.24f + age / 65f'):
        assert needle in burst, needle
    # Rank glitter: a named speed multiplier 15-20% below the original 1900 ms cycle, and an endpoint glint that swells as well as brightens.
    assert 0.80 <= constant(glitter, 'GlitterSpeed') <= 0.85 and constant(glitter, 'BaseCycleMilliseconds') == 1900
    assert 'CycleMilliseconds = (int)(BaseCycleMilliseconds / GlitterSpeed + 0.5f);' in glitter
    assert '(elapsed % CycleMilliseconds) / (float)CycleMilliseconds * step' in glitter and 'elapsed % 1900' not in glitter
    assert 'tipClip.Area = track;' in glitter and 'elapsed % TipCycleMilliseconds' in glitter
    assert constant(glitter, 'TipMaxSide') > constant(glitter, 'TipMinSide') > 0 and constant(glitter, 'TipMinOpacity') >= 0.4
    assert 'Math.Max(core, Math.Min(track.Height - core, fill.TopLeft.y - track.TopLeft.y))' in glitter, 'The glint stays inside the track at both ends'
    # HUD pulse: same 1600 ms cycle, a slightly wider peak that still fits the 36x30 slot, and a lower opacity range.
    assert 1.8 < constant(pulse, 'PulseExpansion') <= 2.8, 'The ring peak (27.2 + expansion) must stay inside the 30-unit slot'
    assert constant(pulse, 'PulseMinOpacity') < 0.28 and 0.6 <= constant(pulse, 'PulseMaxOpacity') < 0.99
    # The Magical Power effect redraws opacity only. Both stock
    # drawable windows stay within the current fill at every phase.
    import journal_layouts as jl
    assert constant(shimmer, 'Layers') == len(jl.HUD_SPARKLES)
    sparkles = control(root, jl.STATUS_POWER + 8)
    assert flags(sparkles) & 0x411 == 0x410, 'Hidden, clipped, click-through viewport'
    assert not effect_values(sparkles), 'No layout anchor overrides the runtime viewport'
    layers = sparkles.findall("prop[@name='Children']/object")
    assert [control_id(n) for n in layers] == [
        jl.STATUS_SPARKLE_FIRST + i for i in range(len(jl.HUD_SPARKLES))
    ]
    for i, node in enumerate(layers):
        check_animation_drawable(node, int(jl.HUD_SPARKLES[i], 16))
        assert flags(node) & 0x411 == 0x410, 'Each layer starts hidden and clips its drawable'
    assert 'clip.Area = fill;' in shimmer and 'animation.Start();' in shimmer
    assert 'layer.Area = local;' in shimmer
    frame = shimmer.split('private bool Paint(int elapsed)')[1].split('internal void Stop()')[0]
    assert 'Math.Cos' in frame and 'layer.ShadeColor' in frame and '.Area' not in frame
    assert 'row.Sparkles.SetFill(bounds);' in header and 'row.Sparkles.Stop();' in header
    assert window.count('header.StopAnimations();') == 2
    assert clock.count('Simulator.AddObject(') == 1 and 'Simulator.DestroyObject(ObjectId);' in clock
    assert 'System.Environment.TickCount' in clock and 'public override void Simulate()' in clock
    stage = page.split('private void StagePerk(Perk perk)')[1].split('internal void RefreshProgress()')[0]
    assert 'selection.StageNext(perk))' in stage and 'feedback.Play(tiles[perk.Index]);' in stage
    assert stage.index('selection.StageNext') < stage.index('Refresh()') < stage.index('feedback.Play')
    assert 'feedback.Preview(' not in page and 'ReportStarTicks(' not in window
    assert 'Staged = "ui_cart_add_to"' in sounds and 'JournalSounds.Play(JournalSounds.Staged)' in feedback
    assert page.count('feedback.Play(') == 1 and 'feedback.Release();' in page
    assert 'StopAnimations();' in page and 'perkPage.StopAnimations();' in window
    assert 'perkPage.Release();' in window and 'glitter.SetFill(progressFill.Area, track);' in page
    assert 'hasPoints && PanelShowing' in entry and 'PerkPoints.Available(identity)' in entry
    assert 'ReservedPerkPoints(identity.SimDescriptionId)' in entry
    assert 'buttonWidth' not in pulse and 'elapsed % 1600' in pulse
    assert 'new Sound(' not in sounds and 'CreateSound' not in sounds
    for event in ('OccultUpdated', 'SkillChanged', 'MinuteChanged', 'SimChanged'):
        assert entry.count(f'target.{event} +=') == 1 and entry.count(f'target.{event} -=') == 1, event
    assert 'UnsubscribeFromModel(model);' in entry and 'pulse = null;' in entry and 'pointsStale' in entry
    deps = root_dir/'.deps'/'Sims3'/'UI.dll'
    if deps.exists():
        data = deps.read_bytes()
        for sound in ('ui_cart_add_to', 'ui_hardwindow_close', 'ui_lifetime_reward_buy', 'ui_secondary_button'):
            assert sound.encode('utf-16le') in data, f'The sound name {sound} is not in the game UI assembly'
    check_namespaces(root_dir)
    if check_game_api.check(root_dir):
        print('PASS: public game API and reflected native members verified against .deps assemblies')
    else:
        print('SKIPPED: game API check needs the reference assemblies in .deps/Sims3')
    print('PASS: sparkler baseline, HUD pulse, rank glitter and Magical Power shimmer resource bindings, clipping, input, layering, tuning constants and lifecycle wiring')
    check_modal_and_purchase(root_dir, window, page, constants)
    check_native_bindings(root_dir, window, entry)


def check_native_bindings(root_dir, window, entry):
    bridge = cs_source.class_text(root_dir, 'NativeUi')
    for token in ('Delegate.CreateDelegate(HudStateChanged.EventHandlerType, callback.Target, callback.Method)',
                  'evt.GetAddMethod(true)', 'evt.GetRemoveMethod(true)'):
        assert token in bridge
    for source, owner, handler in ((window, 'root', 'OnWindowShutdown'), (entry, 'panel', 'OnPanelShutdown')):
        add_owner = 'active' if owner == 'panel' else owner
        assert f'NativeUi.WatchShutdown({add_owner}, {handler}, true)' in source
        assert f'NativeUi.WatchShutdown({owner}, {handler}, false)' in source
    assert 'viewport.AreaChange += OnViewportChanged;' in window and 'viewport.AreaChange -= OnViewportChanged;' in window
    assert 'panel.AreaChange += OnAreaChange;' in entry and 'panel.AreaChange -= OnAreaChange;' in entry
    assert 'NativeUi.WatchInfoState(current, infoStateChanged, true);' in entry
    assert 'NativeUi.WatchInfoState(hud, infoStateChanged, false);' in entry
    prompt = cs_source.class_text(root_dir, 'PerkPurchaseDialog')
    assert 'NativeUi.WatchShutdown(window, OnWindowShutdown, true);' in prompt
    assert 'NativeUi.WatchShutdown(window, OnWindowShutdown, false);' in prompt
    assert 'StopWatchingWindow();' in prompt and 'base.EndDialog(endID);' in prompt


def check_modal_and_purchase(root_dir, window, page, constants):
    """Native modal state, close and purchase sounds, and the purchase effect: the wiring a static check can see."""
    src = root_dir/'src'
    modal = cs_source.class_text(root_dir, 'JournalModal')
    sounds = cs_source.class_text(root_dir, 'JournalSounds')
    effect = cs_source.class_text(root_dir, 'PurchaseEffect')
    boot = (src/'Core'/'JournalBootstrap.cs').read_text()
    entry = cs_source.class_text(root_dir, 'SimologyEntry')

    # Modal: the calls and the order ModalDialog.StartModal and StopModal use, and one End that is safe to repeat.
    begin = modal.split('internal void Begin()')[1].split('internal void End()')[0]
    order = ['DarkenBackground(true)', 'window.Visible = true', 'UIManager.GetFocus(InputContext.kICKeyboard)', 'UIManager.SetFocus(',
             'AddModalTriggerHook(TriggerMap, TriggerActivationMode.kPermanent, WindowModalPriority)', 'UIManager.BeginModal(window)', 'Pause();']
    positions = [begin.index(item) for item in order]
    assert positions == sorted(positions), 'Begin must follow the order of ModalDialog.StartModal'
    assert 'TriggerMap = "OKCancelDialog"' in modal and 'WindowModalPriority = 18' in modal
    end = modal.split('internal void End()')[1]
    steps = re.search(r'JournalAction\[\] steps = \{ ([^}]*) \}', end).group(1)
    assert steps == 'RemoveHook, EndModal, Undim, Resume, RestoreFocus', 'End must follow the order of ModalDialog.StopModal'
    assert 'if (ended)' in end and 'catch (ResetException' in end and 'throw reset;' in end, 'End must be safe to repeat and must finish every step'
    assert 'UIManager.DarkenBackground(false, UIManager.GetModalWindow() == null)' in modal
    assert 'GameSpeedLocked' in modal and 'LockGameSpeed(Sims3.SimIFace.Gameflow.GameSpeed.Pause)' in modal and 'UnlockGameSpeed()' in modal
    assert modal.count('if (!paused)') == 1 and 'paused = true;' in modal, 'Only a speed lock this window took is released'
    assert 'PushModal' not in modal and 'PopModal' not in modal, 'PushModal and PopModal only act on console builds'
    assert 'IsModal()' not in window, 'The Journal must not reject its own keys because it is modal'
    assert 'IsModal()' in boot and boot.index('window.Focus();') < boot.index('UIManager.IsModal()'), \
        'An open Journal is focused before the modal check can reject it'
    assert 'AddTriggerHook(' not in window and 'RemoveTriggerHook' not in window, 'The modal owns the key map hook'
    assert 'modal.Begin();' in window and window.count('modal.End();') == 2, 'End from Dismiss and from Dispose'
    assert window.index('modal = new JournalModal(root);') < window.index('modal.Begin();') < window.index('Focus();', window.index('modal.Begin();'))

    # Closing: every way out comes through Close once; sounds only on the normal ones; nothing is left modal.
    assert 'JournalAction' in window and 'private void Close(CloseMode mode, bool keyboard)' in window
    close = window.split('private void Close(CloseMode mode, bool keyboard)')[1].split('private void Dismiss()')[0]
    assert close.index('if (closing)') < close.index('closing = true;') < close.index('perkPage.Commit(true)')
    assert 'finally' in close and 'Dismiss();' in close, 'The window must be handed back even if buying throws'
    assert close.count('JournalSounds.Play(') == 2 and 'JournalSounds.Key' in close
    assert 'bought ? JournalSounds.Purchase : JournalSounds.Close' in close, 'One of the two close sounds, never both'
    assert 'if (mode == CloseMode.Confirm)' in close and close.count('perkPage.Commit(') == 1, 'Only the checkmark commits'
    assert 'perkPage.Discard();' in close and close.index('perkPage.Commit(true)') < close.index('perkPage.Discard();') < close.index('JournalSounds.Play(bought ?')
    assert 'if (mode != CloseMode.Abandon)' in close, 'Abandoned windows are silent'
    dismiss = window.split('private void Dismiss()')[1].split('public void Dispose()')[0]
    assert 'root.Visible = false;' in dismiss and 'modal.End();' in dismiss and 'RunLater(delegate { JournalBootstrap.Close(this); })' in dismiss
    assert dismiss.index('modal.End();') < dismiss.index('JournalBootstrap.Close(this)')
    assert window.count('Abandon();') == 5, 'Errors, window shutdown and ineligible selections use silent cleanup'
    assert 'CancelTrigger:' in window and 'Close(CloseMode.Cancel, true);' in window and 'keyActivation' in window, 'Escape cancels, it never commits'
    assert 'closing' in window.split('private void OnClick')[1].split('SetKeyboardButton')[0], 'No clicks once closing'
    assert 'closing' in window.split('private void OnTrigger')[1].split('switch')[0], 'No keys once closing'
    assert window.split('Bind(close, RequestClose)')[0] and 'private void RequestClose()' in window
    assert 'perkPage.Commit()' not in window

    # Sounds: names, and nothing but Audio.StartSound.
    for name, value in (('Close', 'ui_hardwindow_close'), ('Purchase', 'ui_lifetime_reward_buy'), ('Key', 'ui_secondary_button'),
                        ('Staged', 'ui_cart_add_to')):
        assert f'{name} = "{value}"' in sounds, name
    assert 'Audio.StartSound(sound)' in sounds
    assert re.search(r'\bAudio\.', ''.join(p.read_text() for p in src.rglob('*.cs')).replace('Audio.StartSound(sound)', '')) is None, \
        'Every sound goes through JournalSounds'

    # Large, ephemeral flight overlay with a tested native fallback if its layout cannot load.
    assert 'FlightIconSize = 64f' in effect and 'EndIconSize = 46f' in effect
    assert 'NativeLayouts.PurchaseFlight' in effect and 'ScreenToWindow' in effect
    assert 'JournalBootstrap.RunLater(Frame);' in effect and 'NativeLayouts.Release(layout,' in effect
    assert 'IconZoop.Zoop(image,' in effect and 'ResetException' in effect
    flight = ET.fromstring(dict(layouts())[PURCHASE_FLIGHT]).find('object')
    assert flight is not None and flight.get('id') == '0x00000001'
    assert flight.find("prop[@name='FillDrawable']/object").get('cls') == 'ImageDrawable'
    assert flight.find("prop[@name='WindowFlags']").get('value') == '0x00002012'
    commit = page.split('internal bool Commit(bool celebrate)')[1].split('private List<Perk> StagedPerks()')[0]
    assert commit.index('StagedPerks()') < commit.index('selection.Commit()') < commit.index('Celebrate(staged)')
    assert 'if (!selection.Commit())' in commit and commit.index('return false;', commit.index('if (!selection.Commit())')) < commit.index('Celebrate(staged)')
    assert page.count('PurchaseEffect.Fly(') == 1 and page.count('Celebrate(staged)') == 1
    assert 'MostFlights' in page and 'TryButtonCenter' in page and 'bool TryButtonCenter(out Vector2 center)' in entry
    assert 'internal static bool TryButtonCenter(out Vector2 center)' in boot
    assert re.search(r'Journal = "\w+_r78"', constants)
    print('PASS: modal begin/end follow ModalDialog (dim, focus, key map hook, BeginModal, speed lock; reverse on close), End is idempotent, no self-rejection by IsModal')
    print('PASS: one Close path (commit, one sound, hide, End, deferred dispose) with errors and window shutdown silent; large 64px moving icon only after a successful commit, with native IconZoop fallback')


def check_overview(root, root_dir, constants, counts):
    """The five-section Overview: layout controls, source wiring, species-independent data flow and panel geometry."""
    import journal_layouts as jl
    src = root_dir/'src'
    page = cs_source.class_text(root_dir, 'OverviewPage')
    lst = cs_source.class_text(root_dir, 'OverviewList')
    data = overview_text(root_dir)
    window = cs_source.class_text(root_dir, 'JournalWindow')

    # Every Overview control exists exactly once, and the pooled lists match the capacities used by the page.
    singles = [jl.OV_RANK_TITLE, jl.OV_RANK_TEXT, jl.OV_ARTWORK, jl.OV_LIFE_ICON, jl.OV_LIFE_TITLE, jl.OV_LIFE_TEXT,
               jl.OV_RANK_NUMERAL, jl.OV_LIFE_ART]
    singles += [jl.OV_FRAME + i for i in range(10)] + [jl.OV_HEADER_CELL + i for i in range(3)]
    singles += [jl.OV_HEADER_TITLE + i for i in range(3)] + [jl.OV_HEADER_CORNER + i for i in range(6)]
    singles += [jl.OV_HEADER_EDGE + i for i in range(6)]
    for index, rows in enumerate(jl.OV_LIST_ROWS):
        first = jl.OV_LIST_FIRST + index * jl.OV_LIST_STRIDE
        singles += [first + r * jl.OV_ROW_CONTROLS + k for r in range(rows) for k in range(jl.OV_ROW_CONTROLS)]
        singles += [first + jl.OV_SCROLL_OFFSET + k for k in range(5)]
        assert rows * jl.OV_ROW_CONTROLS <= jl.OV_SCROLL_OFFSET, 'Row controls overlap the scrollbar parts'
    for cid in singles:
        assert counts[cid] == 1, f'Overview control {cid:08X} missing'
    assert len(singles) == len(set(singles))
    for name, expected in (('StatisticsRows', jl.OV_LIST_ROWS[0]), ('PerkRows', jl.OV_LIST_ROWS[1]), ('KnowledgeRows', jl.OV_LIST_ROWS[2])):
        assert int(re.search(r'const int ' + name + r' = (\d+);', page).group(1)) == expected, f'{name} differs from the layout pool'
    assert jl.OV_ROW_CONTROLS == 6 == constant(lst, 'RowControls') and 'id + 4' in lst and 'id + 1' in lst and 'id + 5' in lst
    for name in ('OverviewFrame', 'OverviewFirstList', 'OverviewListStride', 'OverviewScrollOffset', 'OverviewRankNumeral',
                 'OverviewLifeArt'):
        assert re.search(r'const uint ' + name + ' = ', constants), name
    for gone in ('OverviewEmblem', 'OverviewLifeDivider', 'OverviewRankBar', 'OverviewRankProgress', 'OverviewRankGradient'):
        assert gone not in constants and gone not in page + data + window, f'{gone} was removed'
    for name, value in (('OverviewRankNumeral', jl.OV_RANK_NUMERAL),
                        ('OverviewLifeArt', jl.OV_LIFE_ART)):
        assert int(re.search(name + r' = (0x[0-9A-Fa-f]+)', constants).group(1), 16) == value, name
    assert int(re.search(r'OverviewListStride = (0x[0-9A-Fa-f]+)', constants).group(1), 16) == jl.OV_LIST_STRIDE
    assert int(re.search(r'OverviewScrollOffset = (0x[0-9A-Fa-f]+)', constants).group(1), 16) == jl.OV_SCROLL_OFFSET
    assert int(re.search(r'OverviewFirstList = (0x[0-9A-Fa-f]+)', constants).group(1), 16) == jl.OV_LIST_FIRST

    # Text flags: labels stay click-through, only the scroll parts and list bodies take the mouse.
    for cid in (jl.OV_RANK_TEXT, jl.OV_LIFE_TEXT):
        node = control(root, cid)
        assert node.find("prop[@name='WordWrap']").get('value') == '4' and node.find("prop[@name='Overflow']").get('value') == '1', 'Multi-line text must wrap and clip'
    def color_of(cid):
        return control(root, cid).find("prop[@name='TextColor']").get('value')

    for i in range(3):
        heading = jl.OV_HEADER_TITLE + i
        assert control(root, heading).find("prop[@name='HorizontalAlign']").get('value') == '1', 'Section headings are centred in their header band'
        assert layout_font(root, heading) == jl.OV_HEADING_FONT, 'Section headings are bold 15pt'
        first = jl.OV_LIST_FIRST + i * jl.OV_LIST_STRIDE
        assert control(root, first + 3).find("prop[@name='HorizontalAlign']").get('value') == '2', 'Values are right-aligned'
        assert control(root, first + 2).find("prop[@name='HorizontalAlign']").get('value') == '1', 'Detail indicators share one centred column'
        empty = first + jl.OV_SCROLL_OFFSET + 4
        assert control(root, empty).find("prop[@name='HorizontalAlign']").get('value') == '1', 'Empty message is centred'
        assert layout_font(root, empty) == jl.OV_NOTE_FONT and color_of(empty) == jl.OV_MUTED, 'Empty states are small and muted'
        expected_font = jl.OV_KNOWLEDGE_FONT if i == jl.OV_KNOWLEDGE_LIST else jl.OV_ROW_FONT
        assert layout_font(root, first + 1) == expected_font and layout_font(root, first + 3) == expected_font
        assert layout_font(root, first + 2) == expected_font
    assert layout_font(root, jl.OV_RANK_TITLE) == jl.OV_RANK_FONT, 'The rank title is bigger than the headings'
    assert jl.OV_RANK_FONT == '0x062eae0e' and jl.OV_HEADING_FONT == jl.FOOTER_FONT == '0x062eae0d'
    for cid in (jl.OV_RANK_TEXT, jl.OV_LIFE_TEXT):
        assert layout_font(root, cid) == jl.OV_BODY_FONT
    # The rank numeral stands alone: no marker frame, well or surround, the largest stock bold style, navy, centred in its own column.
    numeral = control(root, jl.OV_RANK_NUMERAL)
    assert numeral.find("prop[@name='HorizontalAlign']").get('value') == '1' and numeral.find("prop[@name='VerticalAlign']").get('value') == '1'
    assert layout_font(root, jl.OV_RANK_NUMERAL) == jl.OV_NUMERAL_FONT == '0xaf90b579', 'Stock 30px bold LargeButtonCaption style'
    assert color_of(jl.OV_RANK_NUMERAL) == jl.NAVY, 'Navy numeral'
    assert 'StdDrawable' not in ET.tostring(numeral, encoding='unicode') and 'ImageDrawable' not in ET.tostring(numeral, encoding='unicode')
    for removed in ('OV_EMBLEM_WELL', 'OV_EMBLEM', 'OV_EMBLEM_FRAME', 'OV_EMBLEM_NUMERAL', 'OV_LIFE_DIVIDER', 'EMBLEM_SCALE'):
        assert not hasattr(jl, removed), f'{removed} should be gone from the layout generator'
    for text in (page, data):
        assert 'RankEmblem' not in text and 'emblemFrame' not in text and 'MarkerSize' not in text and 'WellSize' not in text and 'lifeDivider' not in text
    assert 'numeral.TextColor = Ink;' in page, 'The skin does not recolour the numeral'
    # Numeral column: centred vertically across the whole panel at the left; the text column starts to its right.
    assert 'journal.Place(numeral, numeralLeft, y, NumeralWidth, height);' in page and 'float numeralLeft = x + RankPadding;' in page
    assert 'float textLeft = numeralLeft + NumeralWidth + RankTextGap;' in page
    assert constant(page, 'NumeralWidth') >= 80
    assert 'numeral.Visible = showNumeral;' in page and 'OverviewProviders.GetRomanNumeral(data.RankLevel)' in page, 'The numeral follows the rank'
    # Life State has no divider; its icon is centred vertically and the text block starts level with the icon's top edge.
    assert 'journal.Place(lifeIcon, iconLeft, iconTop, LifeIconSize, LifeIconSize);' in page
    assert 'float iconTop = y + (float)Math.Floor((height - LifeIconSize) / 2f);' in page
    assert 'float titleTop = y + LifeTextTop;' in page and 'journal.Place(lifeTitle, x + LifeTextLeft, titleTop, textWidth, LifeTitleHeight);' in page
    assert 'float bodyTop = titleTop + LifeTitleHeight;' in page and 'y + height - LifeTextBottom - bodyTop' in page, 'Long descriptions keep their room'
    assert constant(page, 'LifeTextLeft') - ((constant(page, 'LifeIconColumn') - constant(page, 'LifeIconSize')) / 2 + constant(page, 'LifeIconSize')) >= 20
    check_rank_panel(root, root_dir, jl, page, data, window)
    # Draw order: rows and scrollbars, then text, then headers, then the panels, so nothing hides behind a panel.
    order = child_order(root)
    last_list = max(order.index(jl.OV_LIST_FIRST + i * jl.OV_LIST_STRIDE + jl.OV_SCROLL_OFFSET + 4) for i in range(3))
    chain = [last_list, order.index(jl.OV_RANK_NUMERAL),
             order.index(jl.OV_RANK_TITLE)]
    chain += [order.index(jl.OV_RANK_TEXT), order.index(jl.OV_LIFE_TEXT), order.index(jl.OV_HEADER_TITLE),
              order.index(jl.OV_HEADER_CORNER), order.index(jl.OV_HEADER_EDGE), order.index(jl.OV_HEADER_CELL),
              order.index(jl.OV_FRAME)]
    assert chain == sorted(chain) and len(set(chain)) == len(chain), 'Content must draw in front of the panels'
    assert all(order.index(jl.OV_FRAME + 2 * i) < order.index(jl.OV_FRAME + 2 * i + 1) for i in range(5)), 'Outline in front of its body'

    # The Overview never touches the Perks tab or perk logic beyond reading the acquired tiers.
    assert 'Commit' not in data and 'StageNext' not in data and 'UndoTier' not in data and 'CanStage' not in data
    assert 'selection.Acquired(perk)' in data

    # No invented data: the only statistic is the power reading the header already shows.
    assert data.count('data.Statistics.Add') == 1 and 'power.Label, "", power.Caption' in data, 'The only statistic the Overview adds itself is the power reading the header already shows'
    check_knowledge(root, root_dir, jl, data, page, window)
    check_row_alignment(root, root_dir, jl)
    assert 'Random' not in data and 'Random' not in page and 'Random' not in lst
    assert 'header == null ? null : header.PowerField' in window and 'visible && power.Field.Available ? power.Field : null' in cs_source.class_text(root_dir, 'StatusHeader')

    # Rank: the numeral and the rank title follow the saved progression; there is no bar, percentage or maximum-rank message any more.
    numerals = re.search(r'Numerals = \{([^}]*)\}', data).group(1)
    assert [n.strip().strip('"') for n in numerals.split(',')][:5] == ['I', 'II', 'III', 'IV', 'V']
    assert 'data.RankLevel = rank + 1;' in data and 'data.RankTitle = JournalText.Format("Arcana.Journal.Overview.Rank.Header", rank);' in data
    check_text(data, 'Arcana.Journal.Overview.Rank.Header', 'Rank: {0}')
    for gone in ('RankProgressState', 'RankProgressText', 'RankState', 'Maximum Rank Achieved', '% to ', 'Rank progress is not available yet.',
                 'barFill', 'RefreshBars', 'progressText'):
        assert gone not in page + data, f'{gone} belongs to the removed progress bar'
    assert 'RefreshBars' not in window.split('private void LayoutActivePage()')[1].split('private Rect Scaled')[0]
    assert 'selection.RankProgress' not in data, 'The Overview no longer reads the rank progress; the progression system itself is untouched'
    assert 'AccentColor' not in data
    check_attributes(root_dir, data)
    check_overview_hover(root, root_dir, jl, data, page)
    assert 'if (artworkPlaced)' in page and '(data.RankArtwork != null) != artworkPlaced' in page, 'Reserved artwork area leaves no gap while unused'
    for section, phrase in (('Attributes', 'No attributes to display.'), ('Perks', 'No perks acquired yet.'),
                            ('Knowledge', 'No knowledge to display.')):
        check_text(page, 'Arcana.Journal.Overview.Empty.' + section, phrase)
    headings = re.search(r'string\[\] headings = \{(.*?)\}', page).group(1)
    assert [english()[key] for key in re.findall(r'"([^"]+)"', headings)] == ['Attributes', 'Perks', 'Supernatural Knowledge']
    assert 'Core Statistics' not in page + data + cs_source.class_text(root_dir, 'JournalWindow')
    assert 'Selected Perks' not in page + data and '"Selected Perks (" + data.PerkCount' not in page, 'The heading is just Perks, without the count'
    assert 'internal int PerkCount = -1;' in data and 'data.PerkCount = data.Perks.Count;' in data, 'The perk count stays in the data model'
    assert 'titles[1]' not in page.replace('titles[i] = journal.Label', ''), 'Nothing rewrites the Perks heading'

    # Life State icon: the Witch uses the project's hat (the header proficiency icon), through the shared species mapping.
    assert 'OccultAssetData.StatusIcon(primary, false)' in data and 'primary == OccultTypes.Witch && skill != null' in data
    assert 'return OccultMeter.MeterIcon(primary) ?? skill;' in data, 'Other species keep their existing icon'
    assert 'data.LifeStateIcon = OverviewIcons.Species(context.Primary);' in data
    hat = (root_dir/'assets'/'UI'/'Icons'/'Occult'/'Witch'/'Skills'/'WitchSkill.png').read_bytes()
    assert hat[:16] == b'\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR'
    width, height = struct.unpack_from('>II', hat, 16)
    assert width == height > 0, 'The hat is square, so the fixed life-state image keeps its aspect ratio'
    assert 'OccultAssetData.WitchSkillStatus' in cs_source.file_text(root_dir, 'OccultStatusProvider') and 'WitchSkillStatus' in hud_icons.source()
    life_icon = control(root, jl.OV_LIFE_ICON).find(".//prop[@name='ImageDrawableFlags']").get('value')
    assert life_icon == '0x00000003', 'Fixed width and height: native aspect ratio, never stretched'

    # Full-size Life State art: its own square window that stretches the square source to the icon box (no magnified glyph), shared
    # placement with the glyph window, and the glyph only shown while a species has no art.
    art = control(root, jl.OV_LIFE_ART).find(".//object[@cls='ImageDrawable']")
    assert art.find("prop[@name='ImageDrawableFlags']").get('value') == '0x00000000' and art.find("prop[@name='Scale']").get('value') == '1'
    assert 'data.LifeStateArt = OverviewIcons.Art(context.Primary);' in data
    assert 'SetImage(lifeIcon, lifeImage, data.LifeStateArt == null ? data.LifeStateIcon : null);' in page and 'SetImage(lifeArt, lifeArtImage, data.LifeStateArt);' in page
    assert 'journal.Place(lifeIcon, iconLeft, iconTop, LifeIconSize, LifeIconSize);' in page and 'journal.Place(lifeArt, iconLeft, iconTop, LifeIconSize, LifeIconSize);' in page
    assert child_order(root).index(jl.OV_LIFE_ART) < child_order(root).index(jl.OV_LIFE_ICON), 'Both life state windows sit in front of the panel'
    generated = hud_icons.source()
    assert 'internal static ulong LifeStateArt(OccultTypes type)' in generated
    assert jl.LIFE_ICON_SCALE == 3.0 and 'WitchSkillStatus' in generated, 'Glyph magnification and the 24px header hat stay as they were for every other use'
    check_life_state_art(root_dir, hud_icons, generated, src)

    # Provider pipeline: common content first, then the species, species registered in one place.
    assert 'interface IOverviewProvider' in data and 'void Fill(OverviewContext context, OverviewData data);' in data
    assert data.index('common.Fill(context, data);') < data.index('species.Fill(context, data);')
    assert 'case OccultTypes.Witch:' in data and 'return witch;' in data and 'return magical;' in data
    assert 'LifeStateDescriptions.For(identity)' in data, 'Life State selects its description from the Sim identity'
    assert 'JournalText.Format("Arcana.LifeState.Header", identity.LifeState)' in data
    assert 'JournalText.Format("Arcana.Journal.Overview.Rank.Header", rank)' in data
    check_text(data, 'Arcana.LifeState.Header', 'Life State: {0}')
    check_text(data, 'Arcana.Journal.Overview.Rank.Unimplemented', 'Rank progression for this life state has not been added yet.')

    # The window hosts the page without rebuilding it: theme, ink, layout, data refresh on Sim change and on progression, release.
    assert window.index('JournalTheme.ApplyDialog(root, tabs, grimoirePage.Tabs);') < window.index('overviewPage.ApplyInk();')
    assert 'overviewPage = OverviewPage.Create(this);' in window and 'overviewPage.Layout();' in window
    assert 'overviewPage.Show(showDashboard);' in window and 'header == null ? null : header.PowerField, perkPage, grimoirePage)));' in window
    assert 'RefreshOverview();' in window.split('private void UpdateHeaderFromHud()')[1].split('private static float TabWidth')[0], 'Power changes refresh the Overview'
    progression = window.split('private bool RefreshProgression()')[1].split('private void UpdateHeaderFromHud()')[0]
    assert 'RefreshOverview();' in progression
    select = window.split('private void SelectPage(int index)')[1].split('private void OnKnowledgeChanged')[0]
    assert 'RefreshOverview();' in select
    assert 'overviewPage.Release();' in window.split('public void Dispose()')[1]
    assert 'bool showDashboard = page == JournalPages.Overview;' in select
    # One window for every page: the Perks size, never a page size of its own.
    assert 'OverviewPage.Window' not in window and 'GrimoireHeight' not in window and 'PageSize(' not in window and 'tallest' not in window
    assert 'width = WindowGeometry.Width;' in window and 'height = WindowGeometry.Height;' in window and 'private static float GetFitScale(float width, float height)' in window

    # Scrolling and clipping: the same stock calls the Grimoire uses; long names are clipped by the label, which has no wrap.
    for call in ('UIManager.SetCaptureTarget(InputContext.kICMouse', 'UIManager.ReleaseCapture(InputContext.kICMouse',
                 'args.MouseWheelDelta', 'UIManager.GetCursorPosition()', 'ScreenToWindow(', 'body.MouseWheel += OnWheel;'):
        assert call in lst, f'OverviewList lacks {call}'
    assert 'rows.Count - slots' in lst and 'Math.Max(24f, ' in lst, 'Any row count scrolls; the thumb never gets tiny'
    assert 'empty.Visible = visible && count == 0;' in lst
    # The scrollbar exists only while the list is longer than its pool, and the rows use the freed width.
    assert 'bool scrollable = shown && max > 0;' in lst
    for part in ('track', 'up', 'down', 'thumb'):
        assert f'{part}.Visible = scrollable;' in lst, f'{part} follows the overflow'
    assert '.Visible = shown;' not in lst.split('private void RefreshScrollbar')[1].split('private static void Tint')[0]
    assert 'barShown ? listWidth - ScrollWidth - StripInset - StripGap : listWidth' in lst and 'if (scrollable != barShown)' in lst
    assert 'names[i].Visible = show;' in lst and 'icons[i].Visible = row.Icon != null;' in lst, 'Missing icons hide, they do not leave a hole of the wrong image'
    assert control(root, jl.OV_LIST_FIRST + 1).find("prop[@name='Overflow']").get('value') == '2', 'Row names clip with an ellipsis'

    # Geometry in the shared window: the panels sit inside the content region, do not overlap and keep the 60/40 split.
    geometry = cs_source.file_text(root_dir, 'WindowGeometry')
    width, height = constant(geometry, 'Width'), constant(geometry, 'Height')
    assert (width, height) == (1000.0, 860.0), 'The Perks size is the window'
    content_left, content_top = constant(geometry, 'ContentLeft'), constant(geometry, 'ContentTop')
    content_right, content_bottom = constant(geometry, 'ContentRight'), constant(geometry, 'ContentBottom')
    inset = constant(geometry, 'ContentInset')
    for source_line in ('PanelTop = WindowGeometry.ContentTop;', 'PanelBottom = WindowGeometry.ContentBottom;', 'PowersPanelLeft = WindowGeometry.ContentLeft;', 'PowersPanelRight = WindowGeometry.ContentRight;'):
        assert source_line in geometry, 'The Perks panel is the shared content region: ' + source_line
    values = {name: constant(page, name) for name in ('HeaderHeight', 'BottomEdge', 'Gap', 'TopInset', 'LeftShare',
                                                      'LifeMinimumHeight', 'OutlineInset', 'BottomGap')}
    row = constant(lst, 'RowHeight')
    upper_slots, lower_slots = constant_int(page, 'UpperSlots'), constant_int(page, 'LowerSlots')
    assert upper_slots <= jl.OV_LIST_ROWS[0] and lower_slots <= jl.OV_LIST_ROWS[1] and lower_slots <= jl.OV_LIST_ROWS[2], 'The panels never show more rows than the pools hold'
    pane = content_right - content_left - 2 * inset
    left = int((pane - values['Gap']) * values['LeftShare'] + 0.5)
    right = pane - values['Gap'] - left
    upper = values['HeaderHeight'] + upper_slots * row + values['BottomEdge']
    lower = values['HeaderHeight'] + lower_slots * row + values['BottomEdge']
    top = content_top + values['TopInset']
    lower_top = top + upper + values['Gap']
    life_top = lower_top + lower + values['Gap']
    bottom = content_bottom - values['BottomGap']
    life = max(values['LifeMinimumHeight'], bottom - life_top)
    assert abs(left / pane - 0.6) < 0.02, 'Left column is about 60%'
    assert jl.OV_LIST_ROWS[1] == jl.OV_LIST_ROWS[2], 'Perks and Knowledge share one row count so the bottom edges align'
    assert life_top + life <= bottom + 0.01 and life >= 130, f'Life State panel is {life}'
    assert content_left + inset - values['OutlineInset'] >= content_left and content_left + inset + pane + values['OutlineInset'] <= content_right
    assert content_top < top and life_top + life <= content_bottom, 'Panels stay inside the content region'
    assert upper < 200 and lower < 270 and upper + lower + life + 2 * values['Gap'] <= content_bottom - top, 'A compact dashboard, much shorter than the old one'
    assert right > 300 and left > 450
    print('PASS: Overview: %d controls once each; panels %dx%d / %dx%d, Life State %dx%d at y=%d in the shared %dx%d window'
          % (len(singles), left, upper, right, lower, pane, life, life_top, width, height))


def check_knowledge(root, root_dir, jl, data, page, window):
    """Supernatural Knowledge: species-supplied category rows reading the Grimoire's per-Sim entries, refreshed on every change."""
    src = root_dir/'src'
    grimoire = cs_source.file_text(root_dir, 'GrimoirePage')
    lst = cs_source.class_text(root_dir, 'OverviewList')
    names = re.search(r'KnowledgeNames = \{([^}]*)\}', data).group(1)
    names = [english()[key] for key in re.findall(r'JournalText.Get\("([^"]+)"\)', names)]
    assert names == ['Known ' + n for n in GRIMOIRE_CATEGORY_NAMES] == ['Known Spells', 'Known Curses', 'Known Rituals', 'Known Abilities'], names
    add = data.split('private static void AddKnowledge(OverviewContext context, OverviewData data)')[1].split('// The Grimoire\'s categories in tab order.')[0]
    assert 'data.Knowledge.Add(new OverviewRow(context.KnowledgeIcon(category), KnowledgeNames[category], "",' in add
    assert 'context.KnowledgeCount(category).ToString()' in add and 'category < KnowledgeNames.Length' in add
    assert data.count('data.Knowledge.Add') == 1, 'One shared routine adds the knowledge rows'
    assert add.count('Random') == 0 and not re.search(r'OverviewRow\([^;]*"\d+"', add), 'No fabricated counts'
    # Witch, Fairy and Genie share the same four categories (one definition, one provider routine); nothing else gets them.
    witch = data.split('private sealed class WitchOverview')[1]
    fill = witch.split('public void Fill')[1]
    assert fill.lstrip('(OverviewContext context, OverviewData data)\n {').startswith('AddKnowledge(context, data);'), 'Witch adds the shared rows before the progression check'
    assert 'selection.Available' in fill.split('PowerSelection selection = context.Witch;')[1][:200]
    magical = data.split('private sealed class MagicalOverview')[1].split('// Witch:')[0]
    assert 'AddKnowledge(context, data);' in magical and 'Perks.Add' not in magical and 'RankDescription' not in magical, 'Fairy and Genie get the categories only'
    assert re.search(r'case OccultTypes\.Witch:\s*return witch;\s*case OccultTypes\.Fairy:\s*case OccultTypes\.Genie:\s*return magical;\s*default:\s*return null;', data), 'Only Witch, Fairy and Genie are registered'
    for other in ('Vampire', 'Werewolf', 'Mermaid', 'Ghost'):
        assert f'case OccultTypes.{other}' not in data.split('private static IOverviewProvider For')[1].split('internal static OverviewData Build')[0], other
    # The Grimoire is available to exactly those species, so the rows have a page behind them.
    pages = cs_source.file_text(root_dir, 'JournalPages')
    assert 'new JournalPage(OccultTypes.Witch | OccultTypes.Fairy | OccultTypes.Genie, JournalText.Get("Arcana.Journal.Tab.Grimoire"), "")' in pages
    check_text(pages, 'Arcana.Journal.Tab.Grimoire', 'Grimoire')
    # Species supply the categories: the shared pages and the common provider carry no species rows.
    assert 'Known ' not in page + lst
    assert 'Witch' not in page, 'The shared Overview page has no species names'
    assert 'Witch' not in lst
    # Counts come from the Grimoire's per-Sim entry lists: locked or undiscovered entries are never in them.
    assert re.search(r'internal int GetEntryCount\(int index\)\s*\{\s*return index < 0 \|\| index >= CategoryCount \? 0 : entries\[index\]\.Count;', grimoire)
    assert 'icons[index].Drawable as ImageDrawable' in grimoire and 'internal UIImage GetCategoryIcon(int index)' in grimoire
    icon_art = re.search(r'GrimoireFirstIcon = (0x[0-9A-Fa-f]+)', (src/'Core'/'NativeLayouts.cs').read_text())
    assert icon_art is not None and 'NativeLayouts.GrimoireFirstIcon + (uint)i' in grimoire, 'The tab icon windows are the ones the rows read'
    assert 'return grimoire == null ? 0 : grimoire.GetEntryCount(category);' in data
    assert 'return grimoire == null ? null : grimoire.GetCategoryIcon(category);' in data
    # Refresh: learning (SetEntries) and Sim changes (Reset, then the page refresh) both reach the Overview.
    set_entries = grimoire.split('internal void SetEntries')[1].split('internal void ClearEntries')[0]
    assert set_entries.rstrip().endswith('if (EntriesChanged != null) EntriesChanged(this, EventArgs.Empty);\n        }'.rstrip()) or \
        set_entries.rstrip().split('\n')[-2].strip() == 'if (EntriesChanged != null) EntriesChanged(this, EventArgs.Empty);'
    assert 'ClearEntries(int index) { SetEntries(index, null); }' in grimoire, 'Clearing a category also raises the change'
    assert 'internal event EventHandler EntriesChanged;' in grimoire
    created = window.index('overviewPage = OverviewPage.Create(this);')
    assert created < window.index('grimoirePage.EntriesChanged += OnKnowledgeChanged;'), 'Subscribed once, after both pages exist'
    assert window.count('EntriesChanged +=') == 1 and window.count('EntriesChanged -=') == 1
    assert window.index('grimoirePage.EntriesChanged -= OnKnowledgeChanged;') < window.index('grimoirePage.Release();')
    assert re.search(r'private void OnKnowledgeChanged\(object sender, EventArgs e\)\s*\{\s*RefreshOverview\(\);', window)
    refresh = window.split('private void RefreshSim(SimIdentity next)')[1].split('// The rank text is its own label')[0]
    assert refresh.index('grimoirePage.Reset();') < refresh.index('SelectPage(page);'), 'A Sim change clears the entries, then refreshes the dashboard'
    assert 'if (changed && grimoirePage != null)' in refresh
    reset = grimoire.split('internal void Reset()')[1].split('internal void SetEntries')[0]
    assert 'entries[i].Clear();' in reset, 'The previous Sim\'s knowledge never carries over'
    select = window.split('private void SelectPage(int index)')[1].split('private void OnKnowledgeChanged')[0]
    assert 'overviewPage.Show(showDashboard);' in select and 'RefreshOverview();' in select
    # Rows: icon, name, value, the same row layout as Attributes (one shared list class), 0 included.
    assert 'lists[2] = new OverviewList(journal, GetListId(2), KnowledgeRows, bodies[Knowledge],' in page
    assert 'JournalText.Get("Arcana.Journal.Overview.Empty.Knowledge"), 0f, 64f, true, false);' in page
    assert 'lists[2].SetRows(data.Knowledge);' in page
    check_text(page, 'Arcana.Journal.Overview.Empty.Knowledge', 'No knowledge to display.')
    assert 'empty.Visible = visible && count == 0;' in lst
    assert 'icons[i].Visible = row.Icon != null;' in lst
    print('PASS: Knowledge: %s from %s, species-supplied, counted from the Grimoire entries, refreshed on learning and on Sim change' % (', '.join(names), 'the Grimoire tab icons'))


def check_attributes(root_dir, data):
    """Attributes: percentages come only from registered sources, so no figure is ever invented; none is registered in a normal build."""
    src = root_dir/'src'
    sources = cs_source.class_text(root_dir, 'ModifierReading') + cs_source.class_text(root_dir, 'AttributeSources')
    page = cs_source.class_text(root_dir, 'OverviewPage')
    assert 'AttributeSources.AddRows(context, data);' in data.split('class CommonOverview')[1].split('class WitchOverview')[0], 'Added for every species by the common provider'
    assert 'Regeneration' not in data and 'Witch' not in sources and 'Rest for the Wicked' not in sources + data, 'No Witch-specific statistic is hard-coded into the shared Overview'
    assert 'reportedRegeneration' in sources and 'new OverviewRow(icon, regenerationLabel, "", JournalText.Get("Arcana.Common.UnavailableValue"))' in sources, 'Unavailable regeneration must show a dash, never a fake percentage'
    assert 'string regenerationLabel = JournalText.Get(RegenerationKey);' in sources
    check_text(sources, 'Arcana.Journal.Overview.Attributes.PowerRegeneration', 'Power Regeneration')
    check_text(sources, 'Arcana.Common.UnavailableValue', '—')
    assert 'AttributeSources.Register(' not in ''.join(p.read_text() for p in src.rglob('*.cs') if p != cs_source.file_path(root_dir, 'AttributeTestData')), 'No production code registers a made-up figure'
    test = cs_source.file_text(root_dir, 'AttributeTestData')
    assert test.startswith('#if ARCANA_GRIMOIRE_TEST') and test.rstrip().endswith('#endif'), 'The sample figures are fenced behind the test flag'
    assert '#if ARCANA_GRIMOIRE_TEST\n            AttributeTestData.Register();\n#endif' in sources.replace('\r\n', '\n')
    assert 'if (reading == null || !reading.Available)' in sources and 'continue;' in sources, 'A source with no figure adds no row'
    assert 'catch (ResetException)' in sources and 'ReportOnce("Reading "' in sources, 'One failing source cannot blank the Overview'
    # The percentage format, run for real on the same rules as ModifierReading.Text.
    import decimal
    def fmt(p):
        whole = float(decimal.Decimal(repr(p)).quantize(decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))
        exact = abs(p - whole) < 0.05
        shown = whole if exact else float(decimal.Decimal(repr(p)).quantize(decimal.Decimal('0.1'), rounding=decimal.ROUND_HALF_UP))
        return ('-' if shown < 0 else '+') + (('%d' % abs(shown)) if exact else ('%g' % abs(shown))) + '%'
    assert [fmt(v) for v in (0.0, 15.0, -10.0, 12.5, 25.0, -0.2, 0.04)] == ['+0%', '+15%', '-10%', '+12.5%', '+25%', '-0.2%', '+0%']
    assert 'MidpointRounding.AwayFromZero' in sources and 'CultureInfo.InvariantCulture' in sources and 'float.IsNaN(percent)' in sources
    # Attributes rows shrink to fit four rows in the panel, keep the panel's size, and the pool holds enough rows.
    lst = cs_source.class_text(root_dir, 'OverviewList')
    assert 'lists[0] = new OverviewList(journal, GetListId(0), StatisticsRows, bodies[Statistics],' in page
    assert 'JournalText.Get("Arcana.Journal.Overview.Empty.Attributes"), 0f, 110f, false, true);' in page
    check_text(page, 'Arcana.Journal.Overview.Empty.Attributes', 'No attributes to display.')
    assert 'else if (compact && rows.Count > (int)Math.Floor(listHeight / RowHeight + 0.001f))' in lst
    panel_rows = constant_int(page, 'UpperSlots') * constant(lst, 'RowHeight') / constant(lst, 'MinCompactRow')
    assert panel_rows >= 4.0 and constant_int(page, 'StatisticsRows') >= 4, 'Four attribute rows fit the unchanged panel without a scrollbar'
    # Accuracy: one row directly under regeneration for casters, a dash until a casting system registers a source, one tooltip line per tier up to Legendary.
    accuracy = cs_source.file_text(root_dir, 'AccuracySources')
    assert 'OverviewRow accuracy = AccuracySources.Row(context);' in sources and 'regenerationRow < 0 ? data.Statistics.Count : regenerationRow + 1' in sources, 'Accuracy follows the regeneration row'
    assert 'reading == null ? unavailable : reading.OverallText' in accuracy and 'reading == null ? unavailable : reading.TierText(tier)' in accuracy, 'No source means dashes, never a figure'
    assert 'species == OccultTypes.Witch || species == OccultTypes.Genie || species == OccultTypes.Fairy' in accuracy, 'Only the magically inclined occults show Accuracy'
    assert 'catch (ResetException)' in accuracy and 'ReportOnce("Reading Accuracy"' in accuracy, 'One failing source cannot blank the Overview'
    exempt = {cs_source.file_path(root_dir, 'AttributeTestData')}
    assert 'AccuracySources.Register(' not in ''.join(p.read_text() for p in src.rglob('*.cs') if p not in exempt), 'No production code registers a made-up accuracy'
    assert 'AccuracySources.Register(' in test, 'The sample accuracy is fenced behind the test flag'
    for key, text in (('Attributes.Accuracy', 'Accuracy'), ('Accuracy.Tagline', 'Chance a spell succeeds, by tier.'), ('Accuracy.Line', '{0}: {1}'),
                      ('Accuracy.Tier.1', 'Tier 1'), ('Accuracy.Tier.2', 'Tier 2'), ('Accuracy.Tier.3', 'Tier 3'),
                      ('Accuracy.Tier.4', 'Tier 4'), ('Accuracy.Tier.5', 'Tier 5'), ('Accuracy.Tier.Legendary', 'Legendary')):
        check_text(accuracy, 'Arcana.Journal.Overview.' + key, text)
    check_text(accuracy, 'Arcana.Common.Percent', '{0}%')
    assert accuracy.count('"Arcana.Journal.Overview.Accuracy.Tier.') == 6, 'Tiers 1 to 5 and Legendary'
    assert 'Math.Max(0.0, Math.Min(100.0, percent))' in accuracy and 'MidpointRounding.AwayFromZero' in accuracy and 'CultureInfo.InvariantCulture' in accuracy
    def shown_percent(p):
        clamped = max(0.0, min(100.0, p))
        whole = float(decimal.Decimal(repr(clamped)).quantize(decimal.Decimal('1'), rounding=decimal.ROUND_HALF_UP))
        exact = abs(clamped - whole) < 0.05
        value = whole if exact else float(decimal.Decimal(repr(clamped)).quantize(decimal.Decimal('0.1'), rounding=decimal.ROUND_HALF_UP))
        return (('%d' % value) if exact else ('%g' % value)) + '%'
    assert [shown_percent(v) for v in (85.0, 12.5, 100.0, -3.0, 120.0, 0.04)] == ['85%', '12.5%', '100%', '0%', '100%', '0%']
    print('PASS: Attributes: regeneration row persists as — when no gameplay modifier source exists; genuine registered percentages take its place; four rows fit unchanged panel')
    print('PASS: Accuracy: caster-only row under regeneration, dashes without a registered source, six tooltip tiers (1-5, Legendary), clamped whole percentages')


def check_overview_hover(root, root_dir, jl, data, page):
    """Acquired perks on the Overview: one transparent window per row takes the hover, the tooltip is the Perks page's own builder fed by
    Perk.Describe for the rank bought, and nothing a click could buy is attached."""
    lst = cs_source.class_text(root_dir, 'OverviewList')
    for index, rows in enumerate(jl.OV_LIST_ROWS):
        first = jl.OV_LIST_FIRST + index * jl.OV_LIST_STRIDE
        for row in range(rows):
            base = first + row * jl.OV_ROW_CONTROLS
            hit = control(root, base + 5)
            assert hit.find("prop[@name='FillDrawable']") is None and hit.find("prop[@name='FillColor']").get('value') == '0x00000000', 'The hover window draws nothing'
            flags = int(hit.find("prop[@name='WindowFlags']").get('value'), 16)
            assert flags == 0x2003, 'Same flags as the list body, so it takes the mouse'
            for k in range(5):
                assert int(control(root, base + k).find("prop[@name='WindowFlags']").get('value'), 16) & 0x10, 'The row parts never take the mouse'
    order = child_order(root)
    for index, rows in enumerate(jl.OV_LIST_ROWS):
        first = jl.OV_LIST_FIRST + index * jl.OV_LIST_STRIDE
        for row in range(rows):
            assert order.index(first + row * jl.OV_ROW_CONTROLS + 5) < order.index(jl.OV_FRAME + 1), 'The hover windows sit in front of the panel bodies'
    for part in ('icons[i].IgnoreMouse = true;', 'names[i].IgnoreMouse = true;', 'details[i].IgnoreMouse = true;', 'values[i].IgnoreMouse = true;', 'rules[i].IgnoreMouse = true;', 'hits[i].IgnoreMouse = false;'):
        assert part in lst, part
    assert 'hits[i].MouseWheel += OnWheel;' in lst, 'The wheel scrolls the list over a hover window'
    assert 'hits[i].CreateTooltipCallbackFunction = TooltipFor(i);' in lst and 'hits[i].Click' not in lst and 'hits[i].MouseDown' not in lst, 'Hover only, no click action'
    assert 'PerkTooltip.Create(tip.Title, tip.Tagline, tip.Body, null)' in lst, 'The same tooltip window as the Perks page'
    assert 'journal.Place(hits[i], listLeft, y, rowWidth, rowHeight);' in lst, 'The whole visible row, not just the icon, and never over the scrollbar'
    assert 'int index = hitRows[slot];' in lst and 'rows[index].Tip' in lst, 'Built from the row the pooled window shows now, so it is right after scrolling'
    assert 'hits[slot].Visible = false;' in lst and 'hits[i].Visible = visible && hitRows[i] >= 0;' in lst and 'JournalBootstrap.RunLater' in lst, 'A window that changes rows is hidden first, dropping a stale tooltip'
    assert 'hitsChanged |= UpdateHit(i, show ? index : -1);' in lst, 'Hidden with its list when the page changes'
    assert '!OverviewTip.Same(a.Tip, b.Tip)' in lst, 'Unchanged tips do not rebuild the rows'
    witch = data.split('class WitchOverview')[1]
    assert 'PerkTip(perk, bought, selection.SimName)' in witch and 'perk.Describe(bought - 1, simName)' in witch, 'The description of the rank bought, with the Sim name filled in, from Perk.Describe'
    assert 'perk.Tiers > 1 ? JournalText.Format("Arcana.Journal.Overview.Perks.OwnedRank", bought, perk.Tiers, description) : description' in witch
    check_text(witch, 'Arcana.Journal.Overview.Perks.OwnedRank', 'Rank {0} / {1}\n{2}')
    assert 'new OverviewTip(perk.Name, perk.Tagline, body)' in witch
    assert 'perk.Descriptions' not in data and 'WitchText' not in data, 'Perk text exists in one place only'
    assert 'perk.Describe(selection.Tier(perk), selection.SimName)' in cs_source.file_text(root_dir, 'PowerPage'), 'The Perks page reads the same method'
    print('PASS: Overview perks: a transparent hover window per row (whole row, wheel still scrolls, no click), the Perks page tooltip window and Perk.Describe for the rank bought, hidden and rebuilt when the row or page changes')


def check_row_alignment(root, root_dir, jl):
    """Selected Perks rows: icon and name on the left, indicator column, right-aligned rank with a clear right margin."""
    lst = cs_source.class_text(root_dir, 'OverviewList')
    page = cs_source.class_text(root_dir, 'OverviewPage')
    first = jl.OV_LIST_FIRST + 1 * jl.OV_LIST_STRIDE
    for row in range(jl.OV_LIST_ROWS[1]):
        base = first + row * jl.OV_ROW_CONTROLS
        assert control(root, base + 1).find("prop[@name='HorizontalAlign']").get('value') == '0', 'Names start at the left'
        assert control(root, base + 2).find("prop[@name='HorizontalAlign']").get('value') == '1', 'One centred indicator column'
        assert control(root, base + 3).find("prop[@name='HorizontalAlign']").get('value') == '2', 'Values end at the right margin'
        assert control(root, base + 1).find("prop[@name='Overflow']").get('value') == '2', 'Names ellipsize instead of running into the columns'
    assert constant(lst, 'EdgePadding') >= 20 and constant(lst, 'EdgePadding') > constant(lst, 'Padding')
    assert 'float valueLeft = listLeft + rowWidth - (barShown ? Padding : EdgePadding) - valueWidth;' in lst
    assert 'float detailLeft = valueLeft - detailWidth;' in lst and 'float nameWidth = Math.Max(1f, detailLeft - textLeft - IconGap);' in lst
    assert 'new OverviewList(journal, GetListId(1), PerkRows, bodies[Perks],\n                JournalText.Get("Arcana.Journal.Overview.Empty.Perks"), 56f, 96f, false, false);' in page
    print('PASS: Selected Perks rows: left icon/name, one centred indicator column, right-aligned rank with a %d unit margin' % constant(lst, 'EdgePadding'))


def check_header_bars(root, root_dir, order):
    """Identical clipped stock capsule artwork for both meters, with the glass overlay in front."""
    import journal_layouts as jl
    header = cs_source.class_text(root_dir, 'StatusHeader')
    geometry = cs_source.file_text(root_dir, 'WindowGeometry')
    constants = (root_dir/'src'/'Core'/'NativeLayouts.cs').read_text()
    assert (jl.BAR_ART, jl.BAR_GLASS) == ('9c8c7369cf8efcc7', '56cfed696129b052'), 'Keys as in the stock HUDMotives and HUDSimDisplay meters'
    assert jl.STATUS_TRACK == '0xff263158', 'The empty part of the bar is darker than the stock 0xff3f5088, so a fill in the same blue family stands out'
    assert jl.BAR_EDGE == (0.5,) * 4
    def scale_area(node):
        area = node.find(".//prop[@name='ScaleArea']/struct")
        return [float(p.get('value')) for p in area.findall('prop')]
    for first in (jl.STATUS_PROGRESS, jl.STATUS_POWER):
        gloss, fill, track = (control(root, first + k) for k in (3, 4, 5))
        for node, image in ((gloss, jl.BAR_GLASS), (fill, jl.BAR_ART), (track, jl.BAR_ART)):
            assert image_slots(node, "prop[@name='FillDrawable']/object")[0] == image, 'Stock bar artwork'
            assert node.find(".//prop[@name='ScaleType']").get('value') == '2' and scale_area(node) == [0.5] * 4, 'Nine-slice with 0.5 borders, so both ends stay round'
            assert node.find("prop[@name='FillColor']").get('value') == '0x00000000', 'The tint is the shade colour, as in the stock meters'
        assert track.find("prop[@name='ShadeColor']").get('value') == jl.STATUS_TRACK
        assert fill.find("prop[@name='ShadeColor']").get('value') == jl.STATUS_FILL, 'Placeholder tint; StatusHeader sets the occult colour'
        viewport = control(root, first + 6)
        assert int(viewport.find("prop[@name='WindowFlags']").get('value'), 16) & 0x411 == 0x410
        assert viewport.findall("prop[@name='Children']/object") == [fill], 'The full-sized fill is a direct child of its clip'
        for node in gloss, fill, track:
            assert node.find(".//prop[@name='ScaleFactor']").get('value') == '1,1'
            assert node.find(".//prop[@name='GlowScale']").get('value') == '1,1'
            assert node.find(".//prop[@name='GlowType']").get('value') == '0'
        chain = [order.index(first + 3)] + ([order.index(first + 8)] if first == jl.STATUS_POWER else []) + [order.index(first + 6), order.index(first + 5), order.index(first)]
        assert chain == sorted(chain), 'Glass, sparkle clip, fill clip, track, and text'
    assert 'FillColor' not in header and 'row.Fill.ShadeColor = new Color(fill);' in header and 'row.Fill.ShadeColor.ARGB != fill' in header
    for gone in ('OutlineA', 'OutlineB', 'TrackA', 'TrackB', 'BarInset'):
        assert gone not in header + geometry, f'{gone} was the flat bar'
    assert 'row.Gloss = journal.Control<Window>(first + 3);' in header and 'row.Fill = journal.Control<Window>(first + 4);' in header and 'row.Track = journal.Control<Window>(first + 5);' in header
    assert 'journal.Place(row.Track, x, y, HeaderLayout.BarWidth, HeaderLayout.BarHeight);' in header
    assert 'journal.Place(row.Gloss, x, y, HeaderLayout.BarWidth, HeaderLayout.BarHeight);' in header
    assert 'new HeaderMeterBounds(row.Track.Area, row.Field.Fraction)' in header
    for layer in ('Track', 'Fill', 'Gloss'):
        assert f'SetSliceScale(row.{layer}, journal.LayoutScale);' in header
    assert 'drawable.ScaleAmount = new Vector2(scale, scale);' in header
    assert 'row.FillClip.Area = bounds.Fill;' in header and 'row.Fill.Area = bounds.FillArt;' in header
    witch = cs_source.class_text(root_dir, 'WitchStatus')
    progress = cs_source.class_text(root_dir, 'WitchProgressSource')
    assert 'selection.Proficiency' in witch and 'selection.RankProgress' not in witch
    assert 'skill.Progress' in progress
    assert 'WitchProgression.GetRankState(experience, ladder, record.Claimed)' in progress
    assert 'bounds.Fill.Width <= 0f' in header and 'row.FillClip.Visible = false;' in header
    assert 'uint tint = OccultAssetData.BarTint(primary);' in header and 'Apply(progress, tint);' in header and 'Apply(power, tint);' in header
    assert 'row.Gloss.Visible = show;' in header and 'row.Track.Visible = show;' in header
    assert 'internal const float BarWidth = 104f;' in geometry and 'internal const float BarHeight = 12f;' in geometry, 'Bar size matches the stock 12 unit meter'
    check_bar_tints(root_dir)
    print('PASS: header meters keep the stock capsule and glass with a shared scaled fill viewport and bounded shimmer')


def check_bar_tints(root_dir):
    """Every species' fill is clearly separate from the track, and only the two palette entries that were too close to it changed."""
    import colorsys
    assert hud_icons.BAR_TRACK == 0xFF263158
    palette = dict(hud_icons.PALETTE)
    tints = dict(hud_icons.BAR_TINTS)
    for name, accent in palette.items():
        tint = tints[name]
        distance = hud_icons.bar_distance(tint)
        assert distance >= 50.0, f'{name} fill is too close to the bar track (distance {distance:.1f})'
        if hud_icons.bar_distance(accent) >= hud_icons.BAR_KEEP_DISTANCE:
            assert tint == accent, f'{name} keeps its established colour'
        else:
            assert hud_icons.bar_distance(tint) > hud_icons.bar_distance(accent), f'{name} tint is an improvement on the accent'
    assert {name for name in palette if tints[name] != palette[name]} == {'Witch', 'Mermaid'}, 'Only Witch and Mermaid were too close to the track'
    hue, saturation, value = colorsys.rgb_to_hsv(*(((tints['Witch'] >> s) & 255) / 255.0 for s in (16, 8, 0)))
    assert 235 <= hue * 360 <= 265 and saturation >= 0.6 and value == 1.0, 'Witch proficiency is a saturated purple-blue'
    generated = hud_icons.source().upper()
    assert 'INTERNAL STATIC UINT BARTINT(OCCULTTYPES PRIMARY)' in generated
    assert ', '.join('0x%08X' % tints[name] for name, _ in hud_icons.PALETTE).upper() in generated, 'The generated source carries the tints'
    assert ', '.join('0x%08X' % palette[name] for name, _ in hud_icons.PALETTE).upper() in generated, 'The established accents are unchanged'
    meter = cs_source.class_text(root_dir, 'OccultMeter')
    assert 'OccultAssetData.Accent(type)' in meter and 'BarTint' not in meter, 'The HUD meter keeps the raw accent'
    print('PASS: bar tints: every species separate from the track by distance >= 50 (Witch %.0f, Mermaid %.0f); only Witch and Mermaid brightened' % (hud_icons.bar_distance(tints['Witch']), hud_icons.bar_distance(tints['Mermaid'])))


def check_rank_panel(root, root_dir, jl, page, data, window):
    """The Rank panel: no bar, the numeral, "Rank: X" and the description, on a plain panel."""
    ids = {int(p.get('value'), 16) for p in root.iter('prop') if p.get('name') == 'ControlID'}
    for gone in (0x53E00328 + k for k in range(5)):
        assert gone not in ids, 'The rank bar windows are gone'
    assert 0x53E0032D not in ids, 'The progress caption is gone'
    # The Rank panel is plain again: no gradient control, constant, image, generator or run-time tint remains.
    assert 0x53E0032F not in ids, 'The rank gradient window is gone'
    assert not (root_dir/'tools'/'panel_gradient.py').exists() and not hasattr(jl, 'OV_RANK_GRADIENT') and not hasattr(jl, 'rank_gradient')
    constants = (root_dir/'src'/'Core'/'NativeLayouts.cs').read_text()
    assert 'OverviewRankGradient' not in constants and 'gradient' not in (page + data + window).lower() and 'AccentColor' not in page + data
    order = child_order(root)
    assert order.index(jl.OV_FRAME) < order.index(jl.OV_FRAME + 1), 'The Rank panel is its outline and pale body only'
    assert order.index(jl.OV_RANK_TEXT) < order.index(jl.OV_FRAME) and order.index(jl.OV_RANK_TITLE) < order.index(jl.OV_FRAME) and order.index(jl.OV_RANK_NUMERAL) < order.index(jl.OV_FRAME)
    from ui_icons import resources as ui_resources
    from dbpf import fnv64
    assert fnv64('SupernaturalExpanded.UI.RankGradient') not in {instance for instance, _ in ui_resources()}, 'The gradient image is no longer packaged'
    assert 'FF4D63D3' in hud_icons.source().upper(), 'The Witch accent is the established purple-blue'
    # Title and description: the title and description stack, centred as a group, the description uses the whole text column.
    height_units = constant_int(page, 'UpperSlots') * constant(cs_source.class_text(root_dir, 'OverviewList'), 'RowHeight') + constant(page, 'HeaderHeight') + constant(page, 'BottomEdge')
    body_room = height_units - constant(page, 'RankBottomPadding') - constant(page, 'RankTitleTop') - constant(page, 'RankTitleHeight') - constant(page, 'RankBodyGap')
    assert 10 <= constant(page, 'RankTitleTop') <= 20, 'The compact panel starts its text near the top edge'
    assert body_room >= 5 * 17, f'The compact Rank panel keeps room for a five line description ({body_room})'
    assert 'float bodyTop = y + RankTitleTop + RankTitleHeight + RankBodyGap;' in page
    assert 'float bodyHeight = Math.Max(1f, y + height - RankBottomPadding - bodyTop);' in page
    assert 'journal.PlaceSnapped(rankText, textLeft, bodyTop, textWidth, bodyHeight);' in page
    assert 'journal.PlaceSnapped(rankTitle, textLeft, y + RankTitleTop, textWidth, RankTitleHeight);' in page
    assert 'OverviewList.Reflow(rankText);' in page and 'internal static void Reflow(Text text)' in cs_source.class_text(root_dir, 'OverviewList')
    assert constant(page, 'RankBottomPadding') <= constant(page, 'RankPadding') / 2 + 1, 'The description box runs close to the bottom edge, so a long rank text is not cut off'
    window_text = cs_source.class_text(root_dir, 'JournalWindow')
    assert 'internal void PlaceSnapped(WindowBase control' in window_text and 'snapped.ContainsKey(item.Key) ? Snap(Scaled(item.Value)) : Scaled(item.Value)' in window_text, 'Snapped text keeps whole pixels after a rescale'
    assert layout_font(root, jl.OV_RANK_TEXT) == layout_font(root, jl.OV_LIFE_TEXT) == jl.OV_BODY_FONT, 'The rank paragraph and the Life State paragraph use one style'
    for prop in ('WordWrap', 'Overflow', 'VerticalAlign', 'HorizontalAlign'):
        assert control(root, jl.OV_RANK_TEXT).find(f"prop[@name='{prop}']").get('value') == control(root, jl.OV_LIFE_TEXT).find(f"prop[@name='{prop}']").get('value'), prop
    assert control(root, jl.OV_RANK_TEXT).find("prop[@name='WindowFlags']").get('value') == control(root, jl.OV_LIFE_TEXT).find("prop[@name='WindowFlags']").get('value')
    # Descriptions: the five Witch texts exactly as approved, picked by rank, with the Sim's first name for {Name}.
    approved = [
        "Power isn't everything. Knowledge can best even the strongest warrior, or in this case, the strongest witch. And you want to learn it all! Study, bless, and even curse your way to the top!",
        "Practice makes perfect, and you can already feel your powers blossoming. There is still plenty to learn, but you think you're finally getting the hang of this weird thing called magic.",
        "Magic is no longer a stranger to you. You've earned your place in the magical hierarchy, and you can feel your power growing with each passing moment. Stay on this path, and you might just become the stuff legends are made of!",
        "Years of study and dedication have paid off! {Name} has become a formidable witch, capable of wielding magic that most can only dream of mastering. Even the witches at the very top have taken notice!",
        "{Name} has become a pillar of the magical community! Whenever another witch needs guidance, advice, or a particularly troublesome curse lifted, they'll know exactly who to call.",
    ]
    block = data.split('Descriptions =')[1].split('};')[0]
    found = re.findall(r'^\s*"((?:[^"\\]|\\.)*)",?\s*$', block, re.M)
    found = [english()[key] for key in found]
    assert found == approved, 'The five Witch rank descriptions differ from the approved text'
    assert all(';' not in text for text in found), 'No semicolons'
    assert [('{Name}' in text) for text in found] == [False, False, False, True, True]
    assert 'data.RankDescription = WitchRankDescriptions.For(rank, context.Identity.FirstName);' in data, 'Chosen by the current rank, named after the selected Sim'
    assert 'int rank = (int)selection.Rank;' in data and 'JournalText.Get(Descriptions[index]).Replace(Placeholder, name)' in data and 'Placeholder = "{Name}"' in data
    assert 'Math.Max(0, Math.Min(Descriptions.Length - 1, rank))' in data, 'Never fails on an odd rank'
    assert 'string.IsNullOrEmpty(firstName) ? JournalText.Get("Arcana.Common.ThisSim") : firstName' in data
    check_text(data, 'Arcana.Common.ThisSim', 'This Sim')
    assert len(approved) == 5
    for rank, text in enumerate(approved):
        shown = text.replace('{Name}', 'Gwendolyn')
        assert '{' not in shown and '}' not in shown
    # Rank logic is untouched.
    assert 'data.RankLevel = rank + 1;' in data and 'RankCount = 5' in cs_source.class_text(root_dir, 'WitchProgression')
    print('PASS: Rank panel: no bar or percentage, standalone numeral, Rank title, five approved Witch descriptions with {Name} replaced, plain panel with no gradient')


def check_life_state_art(root_dir, hud_icons, generated, src):
    """Every species ships its 128px Life State art from {Species}/Lifestate/{Species}Lifestate.png, unmodified, in the package."""
    import package_journal
    from dbpf import index, read, write
    from tempfile import TemporaryDirectory
    species = [name for name, _ in hud_icons.PALETTE]
    assert len(species) == 7 and [name for name, _ in hud_icons.LIFE_ART] == species, 'All seven occult types are registered'
    base = root_dir/'assets'/'UI'/'Icons'/'Occult'
    keys = []
    for name, path in hud_icons.LIFE_ART:
        assert path == base/name/'Lifestate'/(name + 'Lifestate.png'), 'Lifestate folder and file name convention'
        assert path.is_file(), f'{name} Life State art is missing at {path.relative_to(root_dir)}'
        data = path.read_bytes()
        hud_icons.validate_life_art(data, name)
        assert f'case OccultTypes.{name}: return {name}LifeStateArt;' in generated, 'The generated source maps the species to its art'
        assert f'internal const ulong {name}LifeStateArt = 0x{hud_icons.life_art_key(name).instance:016X}ul;' in generated
        keys.append(hud_icons.life_art_key(name))
    assert len(set(keys)) == 7 and len({k.instance for k in keys}) == 7
    assert 'LifeState/' not in str(hud_icons.LIFE_ART) and 'WitchHat' not in str(hud_icons.LIFE_ART), 'The outdated path is gone'
    # The package carries the untouched files: write the content resources to a temporary package and read them back.
    resources = package_journal.content_resources()
    by_key = dict(resources)
    for (name, path), key in zip(hud_icons.LIFE_ART, keys):
        assert by_key[key] == path.read_bytes(), 'Packaged byte for byte'
    with TemporaryDirectory() as folder:
        target = Path(folder)/'check.package'
        write(target, resources)
        entries = {entry.key: entry for entry in index(target)}
        for (name, path), key in zip(hud_icons.LIFE_ART, keys):
            assert key in entries and read(target, entries[key]) == path.read_bytes(), f'{name} art missing from the written package'
    # Runtime: the Overview takes the art of the selected Sim's primary species, so it follows the Sim; the stock fallbacks remain.
    data = overview_text(root_dir)
    assert 'data.LifeStateArt = OverviewIcons.Art(context.Primary);' in data and 'OccultAssetData.LifeStateArt(primary)' in data
    assert 'return instance == 0ul ? null : StatusReaders.StatusIcon(instance);' in data and 'default: return 0ul;' in generated, 'No art means the existing glyph fallback'
    assert hud_icons.resources() and len(hud_icons.status_resources()) == 12, 'HUD and skill icons are the same sets as before'
    for name, kind in hud_icons.STATUS_ICONS:
        assert (base/name/'Skills'/(name + kind + '.png')).is_file()
    print('PASS: Life State art: all seven species (%s) are 128x128 RGBA, mapped in OccultAssetData.g.cs and packaged unmodified' % ', '.join(species))


def check_r72(root, root_dir, layout):
    """Perks-page states, the pending footer and the X / checkmark tooltips, and the Grimoire's empty, unselected and selected states."""
    import journal_layouts as jl
    page = cs_source.file_text(root_dir, 'PowerPage')
    selection = cs_source.class_text(root_dir, 'PowerSelection')
    window = cs_source.class_text(root_dir, 'JournalWindow')
    grimoire = cs_source.file_text(root_dir, 'GrimoirePage')
    theme = cs_source.class_text(root_dir, 'JournalTheme')
    natives = (root_dir/'src'/'Core'/'NativeLayouts.cs').read_text()
    # Perk states reuse the existing controls: staged ranks tint their badge, unaffordable costs turn red, exclusions say why.
    assert 'PowersPanel = 11, Pending = 14,' in page, 'Slots 12 and 13 stay unused'
    assert 'SetPending(i, staged);' in page and 'counterPlates[index].ShadeColor = staged ? PendingPlate : plateShade;' in page
    assert '0xFFFFD4D0' in page and '0xFFD42020' in page and 'badgeLeft = perk.Tiers > 1 ? x + 2f : x + (PowerLayout.TileSize - PowerLayout.CounterWidth) / 2f;' in page, 'Pending +1 must be centered and red'
    assert 'SetPerkFaded(i, !open, selection.ShortOnPoints(perk));' in page and 'Color price = unaffordable ? UnaffordableCost : ink;' in page
    assert 'internal bool ShortOnPoints(Perk perk)' in selection and 'internal int PendingCount' in selection
    assert 'ConflictLine(perk)' in page and 'JournalText.Format("Arcana.Journal.Perks.Conflict.Acquired", other.Name)' in page
    check_text(page, 'Arcana.Journal.Perks.Conflict.Acquired', 'Unavailable: incompatible with {0}, which is already acquired.')
    assert 'pendingText.Caption = waiting > 0 ? JournalText.Format("Arcana.Journal.Perks.Pending", waiting) : "";' in page
    assert 'pendingText.Visible = visible && waiting > 0;' in page
    check_text(page, 'Arcana.Journal.Perks.Pending', 'Pending: {0}')
    # The X and checkmark keep their placement and tooltips.
    assert 'cancel.TooltipText = JournalText.Get("Arcana.Journal.Perks.Tooltip.Discard");' in window
    assert 'JournalText.Get("Arcana.Journal.Perks.Tooltip.Confirm")' in window and 'TooltipText = "Confirm"' not in window
    check_text(window, 'Arcana.Journal.Perks.Tooltip.Discard', 'Discard pending perk changes.')
    check_text(window, 'Arcana.Journal.Perks.Tooltip.Confirm', 'Confirm pending perk purchases.')
    assert 'CancelCenterX = 79.5f' in window
    # Grimoire: per-category empty list text, one centered unselected hint, name / Tier / Energy Cost / description once selected.
    for category, phrase, empty_details in (
        ('Spells', 'a spell', 'Spell'), ('Curses', 'a curse', 'Curse'),
        ('Rituals', 'a ritual', 'Ritual'), ('Abilities', 'an ability', 'Ability'),
    ):
        prefix = 'Arcana.Journal.Grimoire.'
        check_text(grimoire, prefix + 'EmptyList.' + category, f'No {category.lower()} to display.')
        check_text(grimoire, prefix + 'Select.' + category, f'Select {phrase} to view its details.')
        check_text(grimoire, prefix + 'EmptyDetails.' + category, f'{empty_details} details will appear here.')
    for name in ('GrimoireDescriptionPrompt', 'GrimoireDescriptionMeta'):
        assert f'NativeLayouts.{name}' in grimoire and f'NativeLayouts.{name}' in theme, name
        assert f'internal const uint {name} =' in natives
    assert control(root, jl.GRIMOIRE_DESC_PROMPT).find("prop[@name='HorizontalAlign']").get('value') == '1', 'The hint is centered'
    assert control(root, jl.GRIMOIRE_DESC_PROMPT).find("prop[@name='TextColor']").get('value') == '0xff5f78a3', 'The hint is muted'
    assert control(root, jl.GRIMOIRE_DESC_META).find("prop[@name='TextColor']").get('value') == '0xff5f78a3'
    assert 'descriptionPrompt.Visible = hint;' in grimoire and 'bool hint = visible && entry == null;' in grimoire
    assert 'descriptionMeta.Visible = visible && entry != null;' in grimoire and 'descriptionName.Visible = visible && entry != null;' in grimoire
    assert 'JournalText.Format("Arcana.Journal.Grimoire.Details.TierAndCost", entry.TierName, cost)' in grimoire
    assert 'JournalText.Format("Arcana.Journal.Grimoire.Details.Cost", cost)' in grimoire
    check_text(grimoire, 'Arcana.Journal.Grimoire.Details.TierAndCost', 'Tier: {0}     Energy Cost: {1}')
    check_text(grimoire, 'Arcana.Journal.Grimoire.Details.Cost', 'Energy Cost: {0}')
    assert 'sorted[category].Count == 0 ? EmptyPrompts[category] : SelectPrompts[category]' in grimoire, 'An empty category does not repeat the list message'
    assert 'journal.Place(descriptionMeta,' in grimoire and 'journal.Place(descriptionPrompt,' in grimoire
    # Each specialization names the colour of its tile's edge gradient in the tree; the shared overlay window carries it, so regular perks and every icon stay as they are.
    witch_tree = cs_source.class_text(root_dir, 'WitchTree')
    assert witch_tree.count('.Conflict = ') == 2 and 'perks[15].Conflict = perks[16];' in witch_tree, 'The two High Witch perks are the only specializations'
    assert witch_tree.count('.Face = ') == 2 and 'perks[15].Face = 0xFF7A45E0;' in witch_tree and 'perks[16].Face = 0xFF2E9E4F;' in witch_tree
    assert 'internal uint Face;' in cs_source.class_text(root_dir, 'Perk')
    assert 'SetEdge(i, perk, !open);' in page and 'if (perk.Face != 0)' in page, 'Only a perk with a Face gets an edge'
    assert 'edge.Visible = false;' in page and 'edges[i] = Add(edge);' in page
    assert 'edge.ShadeColor = new Color(perk.Face);' in page and 'edges[index].ShadeColor = new Color(wanted);' in page
    assert 'tiles[i].ShadeColor' not in page and 'tiles[index].ShadeColor' not in page, 'The tile face is no longer tinted'
    assert 'FaceShade' not in theme and 'StockFace' not in page, 'The whole-tile tint is gone'
    set_icon = page.split('private void SetPerkFaded(')[1].split('private static Color GetFadedColor(')[0]
    assert 'Face' not in set_icon and 'icons[index].ShadeColor = fade ? FadedIcon : LitIcon;' in set_icon, 'The icon shade never involves the specialization colour'
    assert 'Edge = 9' in cs_source.class_text(root_dir, 'PageControl') or 'Edge = 9' in (root_dir / 'src' / 'Occults' / 'OccultPerks.cs').read_text()
    # The generated image: white, transparent in the middle and outside the rounded outline, strongest just inside the outline.
    size, px = generated_rgba(ui_resources.tile_edge_png())
    d = ui_resources.TILE_EDGE_DENSITY
    assert size == (ui_resources.TILE_EDGE_WIDTH * d, ui_resources.TILE_EDGE_HEIGHT * d)
    centre = px[size[0] // 2, size[1] // 2]
    assert centre[3] == 0, 'The middle of the tile shows through'
    assert px[0, 0][3] == 0, 'Outside the rounded corner stays clear'
    top = px[size[0] // 2, int(ui_resources.TILE_EDGE_INSET * d) + 1]
    assert top[:3] == (255, 255, 255) and top[3] == 255, 'A solid outline, white so the shade sets the colour'
    column = [px[size[0] // 2, y][3] for y in range(int(ui_resources.TILE_EDGE_INSET * d), int((ui_resources.TILE_EDGE_INSET + ui_resources.TILE_EDGE_DEPTH) * d))]
    assert column == sorted(column, reverse=True) and column[-1] < 12, 'Solid ring, then a fading glow'
    print('PASS: perk states, pending footer, native-centred X/checkmark, Grimoire empty/selection states')
    print('PASS: specialization perks get an edge gradient in the colour their tree names; tile face, icon and regular perks untouched')


def check_perk_navigation(root_dir, window):
    prompt = cs_source.class_text(root_dir, 'PerkPurchaseDialog')
    page = cs_source.class_text(root_dir, 'PowerPage')
    purchase = cs_source.class_text(root_dir, 'PowerSelection').split('internal bool Commit()')[1]
    assert 'delegate { RequestPage(tab); }' in window
    selection = window.split('private PowerSelection ActiveSelection')[1].split('private ', 1)[0]
    assert 'page == JournalPages.Perks' in selection
    request = window.split('private void RequestPage(int index)')[1].split('private void ResolvePerkNavigation')[0]
    assert 'index == page || !tabs[index].Visible || perkConfirmation != null' in request
    assert request.index('pending == null || pending.PendingCount == 0') < request.index('SelectPage(index);')
    assert request.index('perkConfirmation = prompt;') < request.index('prompt.Open()')
    assert 'JournalBootstrap.RunLater(delegate { ResolvePerkNavigation(prompt, index, pending, choice); });' in request
    resolve = window.split('private void ResolvePerkNavigation')[1].split('private void DismissPerkConfirmation')[0]
    for marker in ('perkConfirmation != prompt', 'ActiveSelection != pending', 'PerkPurchaseChoice.Dismissed',
                   '!perkPage.Commit(true)', 'perkPage.Discard()'):
        assert resolve.index(marker) < resolve.index('SelectPage(index);'), marker
    assert 'if (!perkPage.Commit(true))\n                    {\n                        SetKeyboardButton(tabs[page]);\n                        return;' in resolve
    assert 'selection.DiscardStaged();' in page and 'if (!selection.Commit())' in page
    assert purchase.index('WitchProgressStore.Purchase(simId, tree, purchased)') < purchase.index('Array.Clear(staged')
    assert 'if (record == null)\n            {\n                return false;\n            }' in purchase
    assert ': TwoButtonDialog' in prompt and 'StartModal(false);' in prompt
    assert 'endID == OkayID ? PerkPurchaseChoice.Yes' in prompt and 'endID == CancelID ? PerkPurchaseChoice.No' in prompt
    assert 'OnTriggerCancel()' in prompt and 'EndDialog(DismissID);' in prompt and 'base.EndDialog(endID);' in prompt
    check_text(prompt, 'Arcana.Journal.Perks.Confirmation.Message',
               'Would you like to confirm your selected perks before leaving? Selecting No will discard your pending purchases.')
    check_text(prompt, 'Arcana.Common.Yes', 'Yes')
    check_text(prompt, 'Arcana.Common.No', 'No')
    print('PASS: native tab-purchase dialog wiring, one pending destination, commit/discard/dismiss branches and staged batch commit')


def check_journal_eligibility(root_dir, window):
    identity = cs_source.class_text(root_dir, 'SimIdentity')
    entry = cs_source.class_text(root_dir, 'SimologyEntry')
    boot = cs_source.class_text(root_dir, 'JournalBootstrap')
    pages = cs_source.class_text(root_dir, 'JournalPages')
    types = ('Witch', 'Fairy', 'Vampire', 'Werewolf', 'Genie', 'Mermaid', 'Ghost')
    mask = hud_icons.source().split('internal const OccultTypes SupportedTypes =')[1].split(';')[0]
    assert tuple(re.findall(r'OccultTypes\.(\w+)', mask)) == types
    assert 'return types & OccultAssetData.SupportedTypes;' in identity
    assert identity.count('OccultsOf(info,') == 2, 'The HUD and the modal use the same occult reader'
    read = identity.split('internal static SimIdentity Read(IHudModel model)')[1].split('internal static OccultTypes ReadOccults')[0]
    assert read.index('if (types == OccultTypes.None)') < read.index('return new SimIdentity(')

    connect = entry.split('private void Connect()')[1].split('private void OnGameStateChanging')[0]
    ineligible = connect.split('if (SimIdentity.ReadOccults(model, out id) == OccultTypes.None)')[1].split('if (PanelShowing)')[0]
    assert 'RestoreName();' in ineligible and 'ReleaseButton(' in ineligible and 'return;' in ineligible
    assert entry.count('Place();') == 1 and 'Place();' in connect
    for handler in ('OnSimChanged(ObjectGuid id)', 'OnOccultUpdated()'):
        body = entry.split('private void ' + handler)[1].split('}', 1)[0]
        assert 'pointsStale = true;' in body and 'Refresh();' in body

    open_journal = boot.split('private static void Open()')[1].split('internal static void Close(')[0]
    assert open_journal.index('SimIdentity selected = SimIdentity.Read(model);') < open_journal.index('if (selected == null)')
    assert open_journal.index('if (selected == null)') < open_journal.index('candidate.Open(selected);')
    update = window.split('private void UpdateFromHud()')[1].split('private void RefreshSim(')[0]
    assert re.search(r'if \(next == null\)\s*\{\s*Abandon\(\);\s*return;\s*\}', update)
    assert update.index('SimIdentity next = SimIdentity.Read(model);') < update.index('RefreshSim(next);')
    assert 'new JournalPage(OccultAssetData.SupportedTypes,' in pages
    assert 'return (flags & All[page].Occult) != OccultTypes.None;' in pages
    assert {key.removeprefix('Arcana.LifeState.Name.') for key in english()
            if key.startswith('Arcana.LifeState.Name.')} == set(types)
    print('PASS: supported occult flags gate HUD creation, HUD refresh, the Journal modal, and page access')


if __name__=='__main__':main()
