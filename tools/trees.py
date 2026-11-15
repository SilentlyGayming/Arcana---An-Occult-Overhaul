"""The witch perk catalogue and its icon art.

The perk keys are the quoted first arguments of the Power calls in the C# WitchTree type. Art is
optional: put <PerkKey>.png in assets/UI/Icons/Occult/Witch/Powers. Perks without a file keep the native placeholder icon.
"""
import re
from pathlib import Path

from dbpf import fnv64
from cs_source import class_text

ROOT = Path(__file__).resolve().parent.parent
IMAGE = 0x2F7D0004
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
ID_BASE = 0x53E12000


class Tree:
    def __init__(self, source, icons, prefix):
        self.source, self.icons, self.prefix = source, icons, prefix

    def catalog(self):
        text = class_text(ROOT, self.source)
        return re.findall(r'(?<![A-Za-z])Power\("(\w+)"', text)

    def instance(self, perk):
        return fnv64(self.prefix + perk)

    def id(self, kind, index):
        """Control ID: tree base + kind * 0x100 + index (mirrors OccultTree.Id in C#)."""
        return ID_BASE + kind * 0x100 + index

    def icon_path(self, perk):
        return self.icons / (perk + ".png")


WITCH = Tree("WitchTree", ROOT / "assets" / "UI" / "Icons" / "Occult" / "Witch" / "Powers", "SupernaturalExpanded.WitchIcon.")


def icons(tree=WITCH):
    """Returns {catalogue index: (perk, instance, png bytes)} for perks with art."""
    found = {}
    for index, perk in enumerate(tree.catalog()):
        path = tree.icon_path(perk)
        if path.is_file():
            data = path.read_bytes()
            if data[:8] != PNG_SIGNATURE:
                raise ValueError(f"{path} is not a PNG file")
            found[index] = (perk, tree.instance(perk), data)
    return found
