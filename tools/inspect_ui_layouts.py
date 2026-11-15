"""Lists the controls, comments and image keys of stock layouts extracted from a Sims 3 UI package.

Usage: python3 inspect_ui_layouts.py <extracted-directory> <layout-name-substring> [--class Scrollbar]

The extracted directory is only read. Image files are not part of a layout dump, so this
shows which sprite a control uses, never what the sprite looks like.
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def describe(path, only_class):
    data = path.read_bytes().lstrip(b"\xef\xbb\xbf")
    root = ET.fromstring(data)
    print(f"== {path.name}")
    for node in root.iter("object"):
        cls = node.get("cls")
        if only_class and cls != only_class:
            continue
        control = node.find("prop[@name='ControlID']")
        comment = node.find("prop[@name='Comment']")
        images = [p.get("key") for p in node.iter("prop") if p.get("type") == "image" and p.get("key")]
        images += [v.get("key") for v in node.iter("value") if v.get("type") == "image" and v.get("key")]
        print(f"{cls:<16} clsid={node.get('clsid')} id={control.get('value') if control is not None else '-'}"
              f"  {comment.get('value') if comment is not None else ''}")
        for key in images:
            print(f"    image {key}")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1
    folder, needle = Path(sys.argv[1]), sys.argv[2].lower()
    only_class = sys.argv[sys.argv.index("--class") + 1] if "--class" in sys.argv else None
    matches = sorted(p for p in folder.glob("*LAYO.layout") if needle in p.name.lower())
    if not matches:
        print("No matching layout files.")
        return 1
    for path in matches:
        describe(path, only_class)
    return 0


if __name__ == "__main__":
    sys.exit(main())
