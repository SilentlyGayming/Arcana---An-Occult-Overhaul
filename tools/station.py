import json
import struct

from dbpf import Key, STBL, fnv64
from localization import ENGLISH, LOCALE_IDS, encode_table
from trees import ROOT

CATALOG_KEY = Key(0x319E4F1D, 0x70000000, 0x000000000098D726)
CATALOG = ROOT / "assets" / "Catalog" / "AlchemyStation" / "AlchemyStation.catalog"
TEXT = ROOT / "src" / "Localization" / "AlchemyStation.json"
NAME_KEY = "Arcana.Station.Name"
DESCRIPTION_KEY = "Arcana.Station.Description"
FAMILY = fnv64("Arcana.Localization.NativeOverrides") & 0x00FFFFFFFFFFFFFF


def catalog():
    data = CATALOG.read_bytes()
    for key in (NAME_KEY, DESCRIPTION_KEY):
        if data.count(struct.pack("<Q", fnv64(key))) != 1:
            raise ValueError(f"{CATALOG}: expected the {key} hash exactly once")
    return data


def texts():
    data = json.loads(TEXT.read_text(encoding="utf-8"))
    for locale, entry in data.items():
        if locale not in LOCALE_IDS:
            raise ValueError(f"{TEXT}: unknown Sims 3 locale {locale}")
        if (not isinstance(entry, dict) or set(entry) != {"name", "description"}
                or not all(isinstance(text, str) and text.strip() and "\0" not in text for text in entry.values())):
            raise ValueError(f"{TEXT}: {locale} needs a nonempty name and description")
    if ENGLISH not in data:
        raise ValueError(f"{TEXT}: missing {ENGLISH}")
    return data


def table_key(locale):
    return Key(STBL, 0, (LOCALE_IDS[locale] << 56) | FAMILY)


def resources():
    result = [(CATALOG_KEY, catalog())]
    for locale, entry in sorted(texts().items(), key=lambda item: LOCALE_IDS[item[0]]):
        result.append((table_key(locale), encode_table({NAME_KEY: entry["name"], DESCRIPTION_KEY: entry["description"]})))
    return result
