import re
import xml.etree.ElementTree as ET

from hud_icons import BAR_TRACK, PALETTE

from cs_source import file_text
from trees import ROOT as PROJECT, TREES, WITCH, icons
from ui_resources import (GRIMOIRE_CATEGORY_NAMES, grimoire_icon_instance, portrait_mask_key, HALO_INSTANCE, STAR_INSTANCE,
    GLITTER_BODY_INSTANCE, GLITTER_TIP_INSTANCE, TILE_EDGE_INSTANCE)


def _layout_name(constant):
    text = file_text(PROJECT, "NativeLayouts")
    return re.search(constant + r' = "(\w+)"', text).group(1)


DIALOG = _layout_name("Journal")
HUD_BUTTON = _layout_name("HudButton")
PERK_TOOLTIP = _layout_name("PerkTooltip")
PURCHASE_FLIGHT = _layout_name("PurchaseFlight")

ROOT = 0x53E00001
ENTRY = 0x53E00002
TITLE = 0x53E00003
PORTRAIT = 0x53E00004
PORTRAIT_FRAME = 0x53E00005
SIM_NAME = 0x53E00006
OCCULT_RANK = 0x53E0000C
CLOSE = 0x53E00009
CONTENT = 0x53E0000A
CANCEL = 0x53E0000D
FRAME_UPPER = 0x53E00020
FRAME_LOWER_LEFT = 0x53E00022
FRAME_FOOTER = 0x53E00024
ENTRY_GLYPH = 0x53E0000B
PERK_PULSE = 0x53E000ED
FIRST_TAB = 0x53E00010
GRIMOIRE_FIRST_TAB = 0x53E00180
GRIMOIRE_FIRST_ICON = 0x53E00188
GRIMOIRE_FIRST_CAPTION = 0x53E00190
GRIMOIRE_CATEGORY_BLOCK = 8
GRIMOIRE_LIST_PANEL = 0x53E000D0
GRIMOIRE_LIST_BODY = 0x53E000D1
GRIMOIRE_LIST_TITLE = 0x53E000D2
GRIMOIRE_EMPTY_LIST = 0x53E000D4
GRIMOIRE_TIER_HEADER = 0x53E000D7
GRIMOIRE_COST_HEADER = 0x53E000D8
GRIMOIRE_FIRST_COLUMN_BUTTON = 0x53E000D9
GRIMOIRE_TABLE_HEADER = 0x53E000DC
GRIMOIRE_FIRST_SORT_ARROW = 0x53E000DF
GRIMOIRE_FIRST_SORT_ARROW_DOWN = 0x53E000E2
GRIMOIRE_DESC_PANEL = 0x53E000E5
GRIMOIRE_DESC_BODY = 0x53E000E6
GRIMOIRE_DESC_NAME = 0x53E000E7
GRIMOIRE_DESC_TEXT = 0x53E000E8
GRIMOIRE_DESC_PROMPT = 0x53E000F0
GRIMOIRE_DESC_META = 0x53E000F1
GRIMOIRE_DESC_COST = 0x53E000F2
GRIMOIRE_FIRST_ROW = 0x53E00100
GRIMOIRE_ROWS = 12
GRIMOIRE_SCROLL_TRACK = 0x53E00140
GRIMOIRE_SCROLL_THUMB = 0x53E00141
GRIMOIRE_SCROLL_UP = 0x53E00142
GRIMOIRE_SCROLL_DOWN = 0x53E00143
STATUS_PANEL = 0x53E00090
STATUS_PROGRESS = 0x53E000A0
STATUS_POWER = 0x53E000B0
STATUS_SPARKLE_FIRST = 0x53E00260
OV_FRAME = 0x53E00300
OV_HEADER_CELL = 0x53E0030A
OV_HEADER_TITLE = 0x53E0030D
OV_RANK_TITLE = 0x53E0031C
OV_RANK_TEXT = 0x53E0031D
OV_ARTWORK = 0x53E00320
OV_LIFE_ICON = 0x53E00321
OV_LIFE_TITLE = 0x53E00322
OV_LIFE_TEXT = 0x53E00323
OV_RANK_NUMERAL = 0x53E00325
OV_LIFE_ART = 0x53E0032E
OV_LIST_FIRST = 0x53E00340
OV_LIST_STRIDE = 0x40
OV_LIST_ROWS = (6, 8, 8)
OV_KNOWLEDGE_LIST = 2
OV_ROW_CONTROLS = 6
OV_SCROLL_OFFSET = 0x30
OV_HEADING_FONT = "0x062eae0d"
OV_RANK_FONT = "0x062eae0e"
OV_NUMERAL_FONT = "0xaf90b579"
OV_ROW_FONT = "0x062eae08"
OV_KNOWLEDGE_FONT = "0x062eae07"
OV_BODY_FONT = "0x062ead00"
OV_NOTE_FONT = "0x062ead06"
OV_MUTED = "0xff5f78a3"
ROW_RULE = "0xffb4cae3"
LIFE_ICON_SCALE = 3.0
TOOLTIP_ROOT = 0x53E00080
TOOLTIP_TITLE = 0x53E00081
TOOLTIP_TAGLINE = 0x53E00082
TOOLTIP_BODY = 0x53E00083
TOOLTIP_CONFLICT = 0x53E00084
TOOLTIP_FIRST_MODIFIER = 0x53E00085
TOOLTIP_MODIFIERS = 40
CONFLICT_RED = "0xffd03030"
TAGLINE_BLUE = "0xff2d8cd8"

COLLECTION_FRAME = "163462cf3324a2a8"
FRAME_MARGINS = (0.5, 0.5, 0.5, 0.5)
CHECK_STATES = ("7a91827e813bb2f2", "ca45721b0a7fbeba", "7a91827e813bb2f2", "d0c25a031bc3b081")
CHECK_GLOW = "191215ac147a4e1c"
HUD_SPARKLES = ("639e96c213071130", "4d8b5bc6550234a2")
RANK_GLITTER_TILES = 10
SPARK_PARTICLES = 3 * 28
CHECK_GLYPH = "e89ab064542f4db2"
CANCEL_STATES = ("ea7394e86250f782", "56f4ea61adc1888a", "ea7394e86250f782", "5a0bd249bc2251d1")
CANCEL_GLYPH = "a11c98cfe0beff5a"
CANCEL_SIZE = 29
MOODLET_FRAME = ("b28416cfec2c43b9", "fc90ad10698be554", "bee1ac10464f8d1a")
MOODLET_ICON = "cf36dac848cc55c5"
TILE_SIZE = 84
RANKS = 5
NOTCHES_PER_RANK = 2
RANK_MARKER_SIZE = 40
PORTRAIT_BORDER = "e6b8ca7ff476cb71"
SORT_ASCENDING_ARROW = "424ae4f366bbd963"
SORT_DESCENDING_ARROW = "5cdfce63208c1e46"
ROW_ALTERNATE = "0x70d2ebf7"
SCROLL_UP_ARROW = ("af9e7e2a22f68356", "5d34b6f318ffd075")
SCROLL_DOWN_ARROW = ("27873013b2788c9d", "2ab402b90bf77cc0")
LIST_BODY = "766f8cac65af1d8e"
SURROUND = "1d22a854c78c91b3"
LIST_OUTLINE = "0db945ce1fe0e14f"
HEADER_CELL = "07beea261832de47"
SCROLL_TRACK = "8af6409d7469288c"
SCROLL_THUMB = "07670b7c0c62a8f7"
SCROLL_UP_GLOW = "56e9bf0598ff09f4"
SCROLL_DOWN_GLOW = "749e2dc29314d1c5"
SCROLL_THUMB_GLOW = "04647583b327cbc3"
HEADER_SLICE = (0.45, 0.333333343, 0.45, 0.333333343)
THUMB_SLICE = (0.333333343, 0.5, 0.333333343, 0.5)
DIALOG_BUTTON = ("efcc8e0b112d4903", "7e688206cd0d5395", "efcc8e0b112d4903", "3bae25dd0a39175a")
SIMOLOGY_BUTTON = ("133438cf8a8b7ca1", "61e57a7da8148ebb", "133438cf8a8b7ca1", "93de5313775182c1")
SKILLS_JOURNAL_ICON = "7f53ab12423911b1"
NAVY = "0xff0b246e"
BLUE = "0xff124f92"
def accent(occult):
    return f"0x{dict(PALETTE)[occult]:08x}"
MILESTONE_GREEN = "0x907fcf9f"
STATUS_PLATE = "0xcfffffff"
BAR_ART = "9c8c7369cf8efcc7"
BAR_GLASS = "56cfed696129b052"
BAR_EDGE = (0.5,) * 4
BAR_GLOW = "1,1"
STATUS_TRACK = "0x%08x" % BAR_TRACK
STATUS_FILL = "0xff2d7bd0"
TABS = 3
IDENTITY_FONT = "0x062eae07"
FOOTER_FONT = "0x062eae0d"


def hex_id(value):
    return f"0x{value:08x}"


def prop(node, name, identifier, kind, value=None, **attributes):
    if value is not None:
        attributes["value"] = str(value)
    return ET.SubElement(node, "prop", name=name, propid=identifier, type=kind, **attributes)


def control(kind, class_id, control_id, flags, export=None, area="0,0,1,1",
            fill="0x00000000", font="0", sound=1):
    node = ET.Element("object", cls=kind, clsid=class_id)
    if export is not None:
        node.set("id", hex_id(export))
    prop(node, "WindowFlags", "0xeec1b000", "uint32", flags)
    prop(node, "ControlID", "0xeec1b001", "uint32", hex_id(control_id))
    prop(node, "CommandID", "0xeec1b002", "uint32", 0)
    prop(node, "Area", "0xeec1b005", "rectf", area)
    prop(node, "FillColor", "0xeec1b006", "uint32", fill)
    prop(node, "ShadeColor", "0xeec1b004", "uint32", "0xffffffff")
    prop(node, "TextFont", "0xeec1b00e", "uint32", font)
    prop(node, "SoundGroup", "0xeec1b010", "uint32", sound)
    return node


def borders(node, name, identifier, values):
    container = prop(node, name, identifier, "struct")
    item = ET.SubElement(container, "struct", cls="Borders", clsid="Borders")
    for index, (side, value) in enumerate(zip(("Left", "Top", "Right", "Bottom"), values), 1):
        prop(item, side, str(index), "float", value)


def image_key(instance):
    return f"2f7d0004:00000000:{instance}" if instance else ""


def standard_drawable(images, stretch="2", edge=(0.333333343,) * 4, glow_mask=None, glow_scale="1.1,1.1"):
    node = ET.Element("object", cls="StdDrawable", clsid="0xb03c196f")
    image_list = prop(node, "Image", "0x0f3ac75e", "image", count="8")
    for image in images + ("",) * (8 - len(images)):
        ET.SubElement(image_list, "value", type="image", key=image_key(image))
    masks = prop(node, "DrawMask", "0x0f3c000f", "image", count="8")
    for _ in range(8):
        ET.SubElement(masks, "value", type="image", key="")
    prop(node, "HitMask", "0x0f3c000d", "hitmask")
    prop(node, "ScaleType", "0xef3c000a", "uint32", stretch)
    prop(node, "ScaleFactor", "0xef3c000b", "pointf", "1,1")
    borders(node, "ScaleArea", "0xef3c000c", edge)
    prop(node, "GlowType", "0x0f3c0010", "uint32", 2)
    if glow_mask:
        prop(node, "GlowMask", "0x0f3c0011", "image", key=image_key(glow_mask))
    prop(node, "GlowScale", "0x0f3c0012", "pointf", glow_scale)
    prop(node, "GlowColor", "0x0f3c0013", "uint32", "0xffffffff")
    return node


def portrait_frame_drawable():
    face = standard_drawable((PORTRAIT_BORDER,))
    face.find("prop[@name='GlowType']").set("value", "1")
    face.find("prop[@name='GlowColor']").set("value", "0x506c9bdb")
    face.find("prop[@name='GlowScale']").set("value", "1.035,1.035")
    return face


def image_drawable(image=None, fixed=False, scale=1, horizontal=3, mask=None):
    node = ET.Element("object", cls="ImageDrawable", clsid="0xcf3c4eae")
    if image:
        prop(node, "Image", "0x4f3c4a26", "image", key=image_key(image))
    if mask:
        prop(node, "DrawMask", "0x4f3c4a2c", "image", key=image_key(mask))
    prop(node, "AlignmentHorizontal", "0x4f3c4a29", "uint32", horizontal)
    prop(node, "AlignmentVertical", "0x4f3c4a28", "uint32", 3)
    prop(node, "ImageDrawableFlags", "0x4f3c4a27", "uint32", "0x00000003" if fixed else "0x00000000")
    prop(node, "Tiling", "0x4f3c4a2a", "uint32", 0)
    prop(node, "Scale", "0x4f3c4a2b", "float", scale)
    return node


def window(identifier, drawable=None, fill="0x00000000", flags="0x00002013", shade=None):
    node = control("Window", "0x4ec1b8d8", identifier, flags, fill=fill)
    if shade is not None:
        node.find("prop[@name='ShadeColor']").set("value", shade)
    if drawable is not None:
        prop(node, "FillDrawable", "0xeec1b007", "object").append(drawable)
    return node


def label(identifier, font="0x062eae06", color=NAVY, top=False, align=0, export=None, area="0,0,1,1",
          flags="0x00002013", multiline=False):
    node = control("Text", "0x6f15f51b", identifier, flags, export=export, area=area, font=font)
    borders(node, "TextBorder", "0x6f43fcce", (0, 0, 0, 0))
    prop(node, "TextColor", "0x6f43fccd", "uint32", color)
    prop(node, "HorizontalAlign", "0x6f43fccf", "uint32", align)
    prop(node, "VerticalAlign", "0x6f43fcd0", "uint32", 0 if top else 1)
    prop(node, "WordWrap", "0x6f43fcd1", "uint32", 4 if top else 0)
    prop(node, "Overflow", "0x6f43fcd2", "uint32", 1 if multiline else 2)
    return node


def button(identifier, hud=False, grimoire=False, tab=False):
    if hud:
        node = control("Button", "0xeeefe2c3", identifier, "0x0000200a",
                       export=1, area="0,0,36,30", font="0x062eae00", sound=4)
        drawing = standard_drawable(SIMOLOGY_BUTTON)
        glyph = window(ENTRY_GLYPH, image_drawable(SKILLS_JOURNAL_ICON, fixed=True), flags="0x00002003")
        glyph.find("prop[@name='Area']").set("value", "0,0,36,30")
        glyph.find("prop[@name='ShadeColor']").set("value", "0xff5278ad")
        children = prop(node, "Children", "0xeec1b00b", "object", count="2")
        children.append(glyph)
        children.append(window(PERK_PULSE, image_drawable(f"{HALO_INSTANCE:016x}"), flags="0x00002012"))
    else:
        node = control("Button", "0xeeefe2c3", identifier, "0x0000200a",
                       font="0x062eae06", sound=2)
        normal, disabled, highlighted, pressed = SIMOLOGY_BUTTON
        if tab:
            states = (normal, disabled, highlighted, normal)
            drawing = standard_drawable(states * 2)
        else:
            drawing = standard_drawable(SIMOLOGY_BUTTON + (pressed, disabled, highlighted, pressed))
    if grimoire and not hud:
        node.find("prop[@name='ShadeColor']").set("value", "0xff9ec1e4")
    prop(node, "ButtonFlags", "0xeec1d002", "uint32", 0)
    prop(node, "ButtonStateFlags", "0xeec1d001", "uint32", 0)
    prop(node, "ButtonDrawable", "0xeec1b007", "object").append(drawing)
    prop(node, "Alignment", "0xeec1d004", "uint32", 1)
    borders(node, "CaptionBorder", "0xeec1d006", (1, 1, 1, 1))
    prop(node, "CaptionOffset", "0xeec1d00a", "pointf", "0,0")
    colors = prop(node, "CaptionColors", "0xeec1d00b", "uint32", count="8")
    for color in ((NAVY,) * 8 if tab else (NAVY, "0xff536987", NAVY, NAVY) * 2):
        ET.SubElement(colors, "value").text = color
    prop(node, "CaptionHAlign", "0xeec1d00c", "uint32", 1)
    prop(node, "CaptionVAlign", "0xeec1d00d", "uint32", 1)
    return node


def native_button(identifier, drawing):
    node = control("Button", "0xeeefe2c3", identifier, "0x0000200a",
                   fill="0x00000000", font="0x062eae06", sound=2)
    prop(node, "ButtonFlags", "0xeec1d002", "uint32", 0)
    prop(node, "ButtonStateFlags", "0xeec1d001", "uint32", 0)
    prop(node, "ButtonDrawable", "0xeec1b007", "object").append(drawing)
    prop(node, "Alignment", "0xeec1d004", "uint32", 1)
    borders(node, "CaptionBorder", "0xeec1d006", (0, 0, 0, 0))
    prop(node, "CaptionOffset", "0xeec1d00a", "pointf", "0,0")
    return node


def list_header(identifier):
    return window(identifier, standard_drawable((HEADER_CELL,), edge=HEADER_SLICE),
                  flags="0x00002003")


def sort_button(identifier):
    drawing = standard_drawable(("", "", HEADER_CELL, HEADER_CELL), edge=HEADER_SLICE)
    node = native_button(identifier, drawing)
    node.find("prop[@name='ShadeColor']").set("value", "0x30305480")
    return node


def scroll_arrow_button(identifier, images, glow_mask):
    return native_button(identifier, standard_drawable(
        (images[0], "", "", images[1]), stretch="1", glow_mask=glow_mask))


def scroll_thumb_button(identifier):
    return native_button(identifier, standard_drawable(
        (SCROLL_THUMB,), stretch="2", edge=THUMB_SLICE, glow_mask=SCROLL_THUMB_GLOW))


def list_body(identifier):
    return window(identifier, standard_drawable((LIST_BODY,)), flags="0x00002003")


def icon_glyph(image, scale, colors):
    glyph = ET.Element("object", cls="IconDrawable", clsid="0xbf7bfe30")
    prop(glyph, "Image", "0x4f3c4a26", "image", key=image_key(image))
    prop(glyph, "AlignmentHorizontal", "0x4f3c4a29", "uint32", 3)
    prop(glyph, "AlignmentVertical", "0x4f3c4a28", "uint32", 3)
    prop(glyph, "ImageDrawableFlags", "0x4f3c4a27", "uint32", "0x00000003")
    prop(glyph, "Scale", "0x4f3c4a2a", "float", scale)
    states = prop(glyph, "StateColors", "0x4f3c4a2b", "uint32", count="8")
    for color in colors:
        ET.SubElement(states, "value").text = color
    return glyph


def state_button(identifier, size, font, drawing):
    node = control("Button", "0xeeefe2c3", identifier, "0x0000200a",
                   area=f"0,0,{size},{size}", font=font, sound=2)
    prop(node, "ButtonFlags", "0xeec1d002", "uint32", 0)
    prop(node, "ButtonStateFlags", "0xeec1d001", "uint32", 0)
    prop(node, "ButtonDrawable", "0xeec1b007", "object").append(drawing)
    prop(node, "Alignment", "0xeec1d004", "uint32", 1)
    borders(node, "CaptionBorder", "0xeec1d006", (1, 1, 1, 1))
    prop(node, "CaptionOffset", "0xeec1d00a", "pointf", "0,0")
    colors = prop(node, "CaptionColors", "0xeec1d00b", "uint32", count="8")
    for color in (NAVY, "0xff536987", NAVY, NAVY) * 2:
        ET.SubElement(colors, "value").text = color
    prop(node, "CaptionHAlign", "0xeec1d00c", "uint32", 1)
    prop(node, "CaptionVAlign", "0xeec1d00d", "uint32", 1)
    return node


def hover_glow(node, color="0x22000000", time=0.12, interpolation=0, ease_in=0):
    glow = ET.Element("object", cls="GlowModulate", clsid="0xa03be1de")
    for name, identifier, kind, value in (
        ("EffectGroupID", "0x4f2b000a", "uint32", 0),
        ("Time", "0x4f2b0000", "float", time),
        ("Delay", "0x4f2b0008", "float", 0),
        ("EndDelay", "0x4f2b0009", "float", 0),
        ("Trigger", "0x4f2b0001", "uint32", 4),
        ("InterpolationType", "0x4f2b0002", "uint32", interpolation),
        ("Ease-In", "0x4f2b0003", "float", ease_in),
        ("Ease-Out", "0x4f2b0004", "float", 0),
        ("Damping", "0x4f2b0005", "float", 0.1),
        ("Autoloop", "0x4f2b0006", "bool", "False"),
        ("LoopType", "0x4f2b0007", "uint32", 0),
        ("Color1", "0xa03be100", "uint32", "0x00000000"),
        ("Color2", "0xa03be101", "uint32", color),
    ):
        prop(glow, name, identifier, kind, value)
    winprocs = node.find("prop[@name='WinProcs']")
    if winprocs is None:
        winprocs = prop(node, "WinProcs", "0xeec1b00c", "object", count="1")
    else:
        winprocs.set("count", str(int(winprocs.get("count", "0")) + 1))
    winprocs.append(glow)
    return node


def tile(identifier):
    normal, hover, _, pressed = DIALOG_BUTTON
    node = state_button(identifier, TILE_SIZE, "0x062eae06", standard_drawable(
        (normal, hover, hover, pressed) * 2))
    return hover_glow(node, "0x16000000")


def perk_icon(identifier, art=None):
    return hover_glow(window(identifier, image_drawable(f"{art[1]:016x}" if art else MOODLET_ICON)), "0x12ffffff")


def tile_edge(identifier):
    return window(identifier, image_drawable(f"{TILE_EDGE_INSTANCE:016x}"), flags="0x00002012")


def marker(identifier):
    normal, hover, pressed = MOODLET_FRAME
    return state_button(identifier, RANK_MARKER_SIZE, "0x062eae07", standard_drawable(
        (normal, normal, hover, pressed, pressed, normal, hover, pressed), stretch="1"))


def checkmark(identifier):
    node = control("Sims3IconButton", "0x062569a8", identifier, "0x0000200b",
                   area="0,0,56,56", fill="0xff7f7f7f", sound=2)
    hover_glow(node, "0xff404040", 0.25)
    prop(node, "ButtonFlags", "0xeec1d002", "uint32", "0x00000000")
    prop(node, "ButtonStateFlags", "0xeec1d001", "uint32", "0x00000000")

    frame = standard_drawable(CHECK_STATES, stretch="1")
    frame.find("prop[@name='HitMask']").set("key", image_key(CHECK_STATES[0]))
    frame.find("prop[@name='GlowType']").set("value", "5")
    glow_mask = ET.Element("prop", name="GlowMask", propid="0x0f3c0011", type="image", key=image_key(CHECK_GLOW))
    frame.insert(list(frame).index(frame.find("prop[@name='GlowScale']")), glow_mask)
    frame.find("prop[@name='GlowColor']").set("value", "0xff404040")

    glyph = icon_glyph(CHECK_GLYPH, 0.9,
                       ("0xff002d74", "0xff7a89a2", "0xff002d74", "0xffffffff") + ("0xffffffff",) * 4)

    drawing = ET.Element("object", cls="IconButtonMultiDrawable", clsid="0xb03c197f")
    layers = prop(drawing, "Drawables", "0x0f3ac75f", "object", count="3")
    layers.extend([frame, glyph])
    ET.SubElement(layers, "value", type="object")
    prop(node, "ButtonDrawable", "0xeec1b007", "object").append(drawing)
    prop(node, "Alignment", "0xeec1d004", "uint32", 1)
    borders(node, "CaptionBorder", "0xeec1d006", (1, 1, 1, 1))
    prop(node, "CaptionOffset", "0xeec1d00a", "pointf", "1,1")
    captions = prop(node, "CaptionColors", "0xeec1d00b", "uint32", count="8")
    for _ in range(8):
        ET.SubElement(captions, "value").text = "0xff000000"
    prop(node, "CaptionHAlign", "0xeec1d00c", "uint32", 1)
    prop(node, "CaptionVAlign", "0xeec1d00d", "uint32", 1)
    prop(node, "CaptionWrap", "0xeec1d00e", "uint32", 0)
    prop(node, "CaptionOverflow", "0xeec1d00f", "uint32", 3)
    prop(node, "ButtonType", "0xeec1d003", "uint32", 1)
    prop(node, "ButtonGroupID", "0xeec1d005", "uint32", 0)
    return node


def cancel_button(identifier):
    node = control("Sims3IconButton", "0x062569a8", identifier, "0x0000200b",
                   area=f"0,0,{CANCEL_SIZE},{CANCEL_SIZE}", fill="0xff7f7f7f", sound=2)
    hover_glow(node, "0x3accedff", 0.23, interpolation=1, ease_in=0.8)
    prop(node, "ButtonFlags", "0xeec1d002", "uint32", "0x00000000")
    prop(node, "ButtonStateFlags", "0xeec1d001", "uint32", "0x00000000")

    frame = standard_drawable(CHECK_STATES, stretch="1", glow_mask=CHECK_GLOW, glow_scale="1.15,1.15")
    frame.find("prop[@name='HitMask']").set("key", image_key(CHECK_STATES[0]))
    frame.find("prop[@name='GlowColor']").set("value", "0x4dffffff")

    glyph = icon_glyph(CANCEL_GLYPH, 0.85,
                       ("0xff002d74", "0xff7a89a2", "0xff002d74", "0xffffffff") + ("0xffffffff",) * 4)

    drawing = ET.Element("object", cls="IconButtonMultiDrawable", clsid="0xb03c197f")
    layers = prop(drawing, "Drawables", "0x0f3ac75f", "object", count="3")
    layers.extend([frame, glyph])
    ET.SubElement(layers, "value", type="object")
    prop(node, "ButtonDrawable", "0xeec1b007", "object").append(drawing)
    prop(node, "Alignment", "0xeec1d004", "uint32", 1)
    borders(node, "CaptionBorder", "0xeec1d006", (1, 1, 1, 1))
    prop(node, "CaptionOffset", "0xeec1d00a", "pointf", "1,1")
    captions = prop(node, "CaptionColors", "0xeec1d00b", "uint32", count="8")
    for _ in range(8):
        ET.SubElement(captions, "value").text = "0xff000000"
    prop(node, "CaptionHAlign", "0xeec1d00c", "uint32", 1)
    prop(node, "CaptionVAlign", "0xeec1d00d", "uint32", 1)
    prop(node, "CaptionWrap", "0xeec1d00e", "uint32", 0)
    prop(node, "CaptionOverflow", "0xeec1d00f", "uint32", 3)
    prop(node, "ButtonType", "0xeec1d003", "uint32", 1)
    prop(node, "ButtonGroupID", "0xeec1d005", "uint32", 0)
    return node


def frame_piece(identifier, two_faces=False):
    clip = window(identifier, flags="0x00002413")
    faces = [window(identifier + 1, standard_drawable((COLLECTION_FRAME,), edge=FRAME_MARGINS))]
    if two_faces:
        faces.append(window(identifier + 2, standard_drawable((COLLECTION_FRAME,), edge=FRAME_MARGINS),
                            flags="0x00002012"))
    prop(clip, "Children", "0xeec1b00b", "object", count=str(len(faces))).extend(faces)
    return clip


def help_face(identifier):
    return window(identifier, standard_drawable(CHECK_STATES, stretch="1"),
                  flags="0x00002003")


def help_question(identifier):
    return label(identifier, font=FOOTER_FONT, color="0xff002d74",
                 align=1, flags="0x00002003")


def panel(identifier, flags="0x00002013"):
    return window(identifier, standard_drawable(("cb261762f82ba3fa",)), flags=flags)


def list_outline(identifier, flags="0x00002013"):
    return window(identifier, standard_drawable((LIST_OUTLINE,)), flags=flags)


def surround_panel(identifier, flags="0x00002013"):
    return window(identifier, standard_drawable((SURROUND,)), flags=flags)


def cost_icon(identifier):
    node = window(identifier, image_drawable(SKILLS_JOURNAL_ICON))
    node.find("prop[@name='ShadeColor']").set("value", BLUE)
    return node


def milestone(identifier):
    return window(identifier, fill=MILESTONE_GREEN)


def status_sparkle(identifier, image, shade):
    return window(identifier, image_drawable(image), flags="0x00002412", shade=shade)


def glow_window(identifier, mask, scale, color, flags):
    face = standard_drawable((), stretch="2", glow_mask=mask, glow_scale=scale)
    face.find("prop[@name='GlowType']").set("value", "1")
    face.find("prop[@name='GlowColor']").set("value", color)
    return window(identifier, face, flags=flags)


def add_effect(node, cls, class_id, values):
    proc = ET.Element("object", cls=cls, clsid=class_id)
    for name, identifier, kind, value in values:
        prop(proc, name, identifier, kind, value)
    procs = node.find("prop[@name='WinProcs']")
    if procs is None:
        procs = prop(node, "WinProcs", "0xeec1b00c", "object", count="0")
    procs.set("count", str(int(procs.get("count")) + 1))
    procs.append(proc)
    return node


def effect_timing(seconds, ease_out, loop, trigger=0):
    return (
        ("EffectGroupID", "0x4f2b000a", "uint32", 0),
        ("Time", "0x4f2b0000", "float", seconds),
        ("Delay", "0x4f2b0008", "float", 0),
        ("EndDelay", "0x4f2b0009", "float", 0),
        ("Trigger", "0x4f2b0001", "uint32", trigger),
        ("InterpolationType", "0x4f2b0002", "uint32", 1),
        ("Ease-In", "0x4f2b0003", "float", 0.5 if loop else 0),
        ("Ease-Out", "0x4f2b0004", "float", ease_out),
        ("Damping", "0x4f2b0005", "float", 0.1),
        ("Autoloop", "0x4f2b0006", "bool", "True" if loop else "False"),
        ("LoopType", "0x4f2b0007", "uint32", 0),
    )


def sparkler_overlay(page):
    overlay = window(page(119), flags="0x00002412")
    children = prop(overlay, "Children", "0xeec1b00b", "object", count=str(SPARK_PARTICLES))
    children.extend(window(page(120 + i), image_drawable(f"{STAR_INSTANCE:016x}"),
                           flags="0x00002012") for i in range(SPARK_PARTICLES))
    return overlay


def rank_glitter_clip(page):
    clip = window(page(40), flags="0x00002412")
    children = prop(clip, "Children", "0xeec1b00b", "object", count=str(RANK_GLITTER_TILES))
    children.extend(window(page(41 + i), image_drawable(f"{GLITTER_BODY_INSTANCE:016x}"),
                           flags="0x00002012") for i in range(RANK_GLITTER_TILES))
    return clip


def rank_glitter_tip(page):
    clip = window(page(64), flags="0x00002412")
    prop(clip, "Children", "0xeec1b00b", "object", count="1").append(
        window(page(65), image_drawable(f"{GLITTER_TIP_INSTANCE:016x}")))
    return clip


def sparkle_clip(first):
    clip = window(first, flags="0x00002412")
    prop(clip, "Children", "0xeec1b00b", "object", count=str(len(HUD_SPARKLES))).extend(
        status_sparkle(STATUS_SPARKLE_FIRST + layer, image, shade)
        for layer, (image, shade) in enumerate(zip(HUD_SPARKLES, ("0xc8ffffff", "0xe0ffffff")))
    )
    return clip


def bar_drawable(image):
    face = standard_drawable((image,), stretch="2", edge=BAR_EDGE, glow_scale=BAR_GLOW)
    face.find("prop[@name='GlowType']").set("value", "0")
    return face


def status_row(first, default_icon=None, glint=False):
    icon, text, value, gloss, fill, track = range(first, first + 6)
    fill_clip = window(first + 6, flags="0x00002412")
    prop(fill_clip, "Children", "0xeec1b00b", "object", count="1").append(
        window(fill, bar_drawable(BAR_ART), shade=STATUS_FILL))
    return [
        window(gloss, bar_drawable(BAR_GLASS)),
        *([sparkle_clip(first + 8)] if glint else []),
        fill_clip,
        window(track, bar_drawable(BAR_ART), shade=STATUS_TRACK),
        window(icon, image_drawable(default_icon)),
        label(text),
        label(value, align=2),
    ]


def status_panel(identifier):
    return window(identifier, standard_drawable((LIST_BODY,)), flags="0x00002003",
                  shade=STATUS_PLATE)


def tree_controls(tree):
    perks = range(len(tree.catalog()))
    art = icons(tree)
    ids = tree.id
    page = lambda slot: ids(6, slot)
    front = [
        *(label(ids(11, i), font="0x062eae07", align=1, flags="0x00002012") for i in range(2) if tree.occult == "Genie"),
        sparkler_overlay(page),
        rank_glitter_tip(page),
        help_question(page(6)),
        help_face(page(10)),
        label(page(7), font=FOOTER_FONT, align=2),
        *(milestone(page(16 + n)) for n in range((RANKS - 1) * NOTCHES_PER_RANK * 3)),
        cost_icon(page(8)),
        label(page(9), font=FOOTER_FONT),
        label(page(14), font=FOOTER_FONT, color=BLUE),
        *(marker(ids(5, i)) for i in range(RANKS)),
        *(tile_edge(ids(9, i)) for i in perks),
        *(label(ids(1, i), align=1) for i in perks),
        *(window(ids(2, i), standard_drawable((DIALOG_BUTTON[0],))) for i in perks),
        *(label(ids(3, i), align=1) for i in perks),
        *(cost_icon(ids(7, i)) for i in perks),
        *(window(ids(4, i), fill="0xffd9eaf6") for i in perks),
        *(perk_icon(ids(8, i), art.get(i)) for i in perks),
    ]
    middle = [tile(ids(0, i)) for i in perks]
    back = [
        *(window(ids(10, i), fill="0xffffffff", flags="0x00002012") for i in range(48) if tree.occult == "Genie"),
        rank_glitter_clip(page),
        window(page(1), fill=accent(tree.occult)),
        window(page(0), fill="0xffc1d7ed"),
        surround_panel(page(11)),
    ]
    return front, middle, back


def transparent_button(identifier):
    return native_button(identifier, standard_drawable(("", "", "", "")))


def grimoire_controls():
    controls = [
        label(GRIMOIRE_LIST_TITLE, font="0x062eae06", color=NAVY,
              align=1, flags="0x00002003"),
        label(GRIMOIRE_TIER_HEADER, font="0x062eae06", color=NAVY,
              align=1, flags="0x00002003"),
        label(GRIMOIRE_COST_HEADER, font="0x062eae06", color=NAVY,
              align=1, flags="0x00002003"),
        label(GRIMOIRE_EMPTY_LIST, font="0x062eae06", color=NAVY,
              align=1, flags="0x00002003"),
        label(GRIMOIRE_DESC_NAME, font="0x062eae07", color=NAVY,
              align=0, flags="0x00002003"),
        label(GRIMOIRE_DESC_META, font="0x062eae06", color=OV_MUTED,
              align=0, flags="0x00002003"),
        label(GRIMOIRE_DESC_COST, font="0x062eae06", color=OV_MUTED,
              align=0, flags="0x00002003"),
        label(GRIMOIRE_DESC_PROMPT, font=OV_NOTE_FONT, color=OV_MUTED,
              align=1, flags="0x00002003"),
        label(GRIMOIRE_DESC_TEXT, font="0x062eae06", color=NAVY,
              top=True, flags="0x00002403", multiline=True),
    ]
    for i in range(3):
        controls.append(window(GRIMOIRE_FIRST_SORT_ARROW + i,
                               image_drawable(SORT_ASCENDING_ARROW, fixed=True, horizontal=2),
                               flags="0x00002003"))
        controls.append(window(GRIMOIRE_FIRST_SORT_ARROW_DOWN + i,
                               image_drawable(SORT_DESCENDING_ARROW, fixed=True, horizontal=2),
                               flags="0x00002003"))
    for i in range(GRIMOIRE_ROWS):
        base = GRIMOIRE_FIRST_ROW + 5 * i
        controls += [
            label(base + 2, font="0x062eae06", color=NAVY,
                  flags="0x00002003"),
            label(base + 3, font="0x062eae06", color=NAVY,
                  align=1, flags="0x00002003"),
            label(base + 4, font="0x062eae06", color=NAVY,
                  align=2, flags="0x00002003"),
            transparent_button(base + 1),
            window(base, fill="0x00000000" if i % 2 == 0 else ROW_ALTERNATE,
                   flags="0x00002003"),
        ]
    controls += [
        scroll_arrow_button(GRIMOIRE_SCROLL_UP, SCROLL_UP_ARROW, SCROLL_UP_GLOW),
        scroll_arrow_button(GRIMOIRE_SCROLL_DOWN, SCROLL_DOWN_ARROW, SCROLL_DOWN_GLOW),
        scroll_thumb_button(GRIMOIRE_SCROLL_THUMB),
        window(GRIMOIRE_SCROLL_TRACK, standard_drawable((SCROLL_TRACK,)), flags="0x00002013"),
    ]
    for i in range(3):
        controls.append(sort_button(GRIMOIRE_FIRST_COLUMN_BUTTON + i))
    controls.append(list_header(GRIMOIRE_TABLE_HEADER))
    for i, category in enumerate(GRIMOIRE_CATEGORY_NAMES):
        controls.append(label(GRIMOIRE_FIRST_CAPTION + i, font="0x062eae06",
                              color=NAVY, align=1, flags="0x00002003"))
        art = f"{grimoire_icon_instance(category):016x}"
        controls.append(window(GRIMOIRE_FIRST_ICON + i,
                               image_drawable(art, fixed=False), flags="0x00002003"))
    for i in range(len(GRIMOIRE_CATEGORY_NAMES)):
        controls.append(button(GRIMOIRE_FIRST_TAB + i, grimoire=True))
    controls += [
        list_outline(GRIMOIRE_DESC_PANEL, flags="0x00002003"),
        list_body(GRIMOIRE_DESC_BODY),
        list_outline(GRIMOIRE_LIST_PANEL),
        list_body(GRIMOIRE_LIST_BODY),
    ]
    return controls


def overview_list(first, rows, font=OV_ROW_FONT):
    track, thumb, up, down, empty = (first + OV_SCROLL_OFFSET + i for i in range(5))
    controls = [
        label(empty, font=OV_NOTE_FONT, color=OV_MUTED, align=1),
        scroll_arrow_button(up, SCROLL_UP_ARROW, SCROLL_UP_GLOW),
        scroll_arrow_button(down, SCROLL_DOWN_ARROW, SCROLL_DOWN_GLOW),
        scroll_thumb_button(thumb),
        window(track, standard_drawable((SCROLL_TRACK,)), flags="0x00002003"),
    ]
    for row in range(rows):
        base = first + row * OV_ROW_CONTROLS
        controls += [
            label(base + 1, font=font, color=NAVY),
            label(base + 2, font=font, color=BLUE, align=1),
            label(base + 3, font=font, color=NAVY, align=2),
            window(base, image_drawable(MOODLET_ICON)),
            window(base + 4, fill=ROW_RULE),
            window(base + 5, flags="0x00002003"),
        ]
    return controls


def overview_controls():
    controls = []
    for index, rows in enumerate(OV_LIST_ROWS):
        controls += overview_list(OV_LIST_FIRST + index * OV_LIST_STRIDE, rows,
                                  OV_KNOWLEDGE_FONT if index == OV_KNOWLEDGE_LIST else OV_ROW_FONT)
    controls += [
        label(OV_RANK_NUMERAL, font=OV_NUMERAL_FONT, color=NAVY, align=1),
        label(OV_RANK_TITLE, font=OV_RANK_FONT, color=NAVY),
        label(OV_RANK_TEXT, font=OV_BODY_FONT, color=NAVY, top=True, flags="0x00002413", multiline=True),
        window(OV_ARTWORK, image_drawable(), flags="0x00002012"),
        window(OV_LIFE_ART, image_drawable(), flags="0x00002012"),
        window(OV_LIFE_ICON, image_drawable(fixed=True, scale=LIFE_ICON_SCALE), flags="0x00002012"),
        label(OV_LIFE_TITLE, font=OV_HEADING_FONT, color=NAVY),
        label(OV_LIFE_TEXT, font=OV_BODY_FONT, color=NAVY, top=True, flags="0x00002413", multiline=True),
    ]
    controls += [label(OV_HEADER_TITLE + i, font=OV_HEADING_FONT, color=NAVY, align=1) for i in range(3)]
    controls += [list_header(OV_HEADER_CELL + i) for i in range(3)]
    for panel_index in range(5):
        controls.append(list_outline(OV_FRAME + 2 * panel_index))
        controls.append(list_body(OV_FRAME + 2 * panel_index + 1))
    return controls


def dialog():
    root = control("Dialog", "0x0f0b8b73", ROOT, "0x0000200e",
                   export=1, area="0,0,896,546", fill="0x00000000")
    pages = [tree_controls(tree) for tree in TREES]
    front = [control for page in pages for control in page[0]]
    middle = [control for page in pages for control in page[1]]
    back = [control for page in pages for control in page[2]]
    children = [
        checkmark(CLOSE),
        cancel_button(CANCEL),
        label(TITLE, font=IDENTITY_FONT),
        label(SIM_NAME, font=IDENTITY_FONT, color=BLUE),
        label(OCCULT_RANK, font=IDENTITY_FONT, color=BLUE),
        window(PORTRAIT, image_drawable(mask=portrait_mask_key())),
        window(PORTRAIT_FRAME, portrait_frame_drawable()),
        *status_row(STATUS_PROGRESS, SKILLS_JOURNAL_ICON),
        *status_row(STATUS_POWER, glint=True),
        status_panel(STATUS_PANEL),
        *(button(FIRST_TAB + i, tab=True) for i in range(TABS)),
        *grimoire_controls(),
        *overview_controls(),
        *front,
        *middle,
        *back,
        surround_panel(CONTENT, flags="0x00002003"),
        frame_piece(FRAME_UPPER),
        frame_piece(FRAME_LOWER_LEFT),
        frame_piece(FRAME_FOOTER, two_faces=True),
    ]
    prop(root, "Children", "0xeec1b00b", "object", count=str(len(children))).extend(children)
    prop(root, "DialogFlags", "0xeec2a002", "uint32", 0)
    prop(root, "DialogDrawable", "0xeec1b007", "object").append(standard_drawable(("",)))
    prop(root, "MinWidth", "0xeec2a009", "float", 0)
    prop(root, "MinHeight", "0xeec2a00a", "float", 0)
    prop(root, "MaxWidth", "0xeec2a00b", "float", 10000)
    prop(root, "MaxHeight", "0xeec2a00c", "float", 10000)
    borders(root, "ClientAreaBorder", "0xeec2a003", (3, 3, 3, 3))
    return root


def purchase_flight():
    root = window(0x53E00200, image_drawable(SKILLS_JOURNAL_ICON), flags="0x00002012")
    root.set("id", hex_id(1))
    root.find("prop[@name='Area']").set("value", "0,0,64,64")
    return root


def perk_tooltip():
    root = panel(TOOLTIP_ROOT, flags="0x00002012")
    root.set("id", hex_id(1))
    root.find("prop[@name='Area']").set("value", "0,0,340,120")
    texts = [
        label(TOOLTIP_TITLE, font="0x062eae07", top=True, area="10,10,330,28"),
        label(TOOLTIP_TAGLINE, color=TAGLINE_BLUE, top=True, area="10,30,330,46"),
        label(TOOLTIP_BODY, top=True, area="10,52,330,110"),
        label(TOOLTIP_CONFLICT, color=CONFLICT_RED, top=True, area="10,112,330,128"),
    ]
    texts.extend(label(TOOLTIP_FIRST_MODIFIER + i, top=True, flags="0x00002012")
                 for i in range(TOOLTIP_MODIFIERS))
    for text in texts:
        text.find("prop[@name='Overflow']").set("value", "0")
    prop(root, "Children", "0xeec1b00b", "object", count=str(len(texts))).extend(texts)
    return root


def encode(root):
    graph = ET.Element("graph", {"class": "Layout", "type": "Layout"})
    graph.append(root)
    ET.indent(graph)
    return b"\xef\xbb\xbf" + ET.tostring(graph, encoding="utf-8", xml_declaration=True)


def layouts():
    return [(DIALOG, encode(dialog())), (HUD_BUTTON, encode(button(ENTRY, hud=True))),
            (PURCHASE_FLIGHT, encode(purchase_flight())),
            (PERK_TOOLTIP, encode(perk_tooltip()))]
