import re
from pathlib import Path

from dbpf import fnv64
from cs_source import class_text

ROOT = Path(__file__).resolve().parent.parent
IMAGE = 0x2F7D0004
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
class Tree:
    def __init__(self, source, icons, prefix, id_base, text_prefix, occult):
        self.source, self.icons, self.prefix = source, icons, prefix
        self.id_base, self.text_prefix, self.occult = id_base, text_prefix, occult

    def catalog(self):
        text = class_text(ROOT, self.source)
        return re.findall(r'(?<![A-Za-z])Power\("(\w+)"', text)

    def instance(self, perk):
        return fnv64(self.prefix + perk)

    def id(self, kind, index):
        return self.id_base + kind * 0x100 + index

    def icon_path(self, perk):
        return self.icons / (perk + ".png")


WITCH = Tree("WitchTree", ROOT / "assets" / "UI" / "Icons" / "Occult" / "Witch" / "Powers", "SupernaturalExpanded.WitchIcon.",
             0x53E12000, "Arcana.Witch.Perk.", "Witch")
GENIE = Tree("GenieTree", ROOT / "assets" / "UI" / "Icons" / "Occult" / "Genie" / "Powers", "SupernaturalExpanded.GenieIcon.",
             0x53E14000, "Arcana.Genie.Perk.", "Genie")
TREES = (WITCH, GENIE)


def icons(tree=WITCH):
    found = {}
    for index, perk in enumerate(tree.catalog()):
        path = tree.icon_path(perk)
        if path.is_file():
            data = path.read_bytes()
            if data[:8] != PNG_SIGNATURE:
                raise ValueError(f"{path} is not a PNG file")
            found[index] = (perk, tree.instance(perk), data)
    return found
