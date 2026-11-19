from dataclasses import dataclass
import struct
import zlib
from pathlib import Path


LAYO = 0x025C95B6
XML = 0x0333406C
S3SA = 0x073FAA07
STBL = 0x220557DA
HEADER_SIZE = 96


@dataclass(frozen=True)
class Key:
    type: int
    group: int
    instance: int

    def __str__(self):
        return f"{self.type:08X}:{self.group:08X}:{self.instance:016X}"


@dataclass(frozen=True)
class IndexEntry:
    key: Key
    offset: int
    size: int
    uncompressed_size: int
    compression: int


def fnv64(name):
    value = 0xCBF29CE484222325
    for byte in name.lower().encode("ascii"):
        value = ((value * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF) ^ byte
    return value


def index(package):
    with Path(package).open("rb") as stream:
        header = stream.read(HEADER_SIZE)
        if len(header) != HEADER_SIZE or struct.unpack_from("<4sII", header) != (b"DBPF", 2, 0):
            raise ValueError(f"{package}: expected a TS3 DBPF 2.0 header")
        count, old_offset, size = struct.unpack_from("<III", header, 0x24)
        version, offset = struct.unpack_from("<II", header, 0x3C)
        if version != 3:
            raise ValueError(f"{package}: unsupported DBPF index version {version}")
        stream.seek(offset or old_offset)
        data = stream.read(size)

    if len(data) != size or size < 4:
        raise ValueError(f"{package}: truncated DBPF index")
    flags = struct.unpack_from("<I", data)[0]
    if flags & ~7:
        raise ValueError(f"{package}: unsupported index flags {flags:#x}")

    shared = {}
    cursor = 4
    for field in range(3):
        if flags & (1 << field):
            shared[field] = struct.unpack_from("<I", data, cursor)[0]
            cursor += 4
    if size != cursor + count * (8 - len(shared)) * 4:
        raise ValueError(f"{package}: DBPF index length does not match entry count")

    rows = []
    for _ in range(count):
        fields = []
        for field in range(8):
            if field in shared:
                fields.append(shared[field])
            else:
                fields.append(struct.unpack_from("<I", data, cursor)[0])
                cursor += 4
        type_id, group, high, low, start, length, expanded, compression = fields
        key = Key(type_id, group, (high << 32) | low)
        rows.append(IndexEntry(key, start, length & 0x7FFFFFFF, expanded, compression & 0xFFFF))
    return rows


def refpack(data, expected_size):
    # Decompresses a RefPack resource in either Sims 3 header variant.
    if len(data) < 2 or data[1] != 0xFB or data[0] & 0x3E != 0x10:
        raise ValueError("Unsupported RefPack header " + data[:4].hex())
    width = 4 if data[0] & 0x80 else 3
    cursor = 2 + (width if data[0] & 1 else 0)
    if len(data) < cursor + width:
        raise ValueError("Truncated RefPack size header")
    if int.from_bytes(data[cursor:cursor + width], "big") != expected_size:
        raise ValueError("RefPack header size disagrees with DBPF index")
    cursor += width
    output = bytearray()

    while cursor < len(data):
        command = data[cursor]
        cursor += 1
        back = count = 0
        if command >= 0xFC:
            output.extend(data[cursor:cursor + (command & 3)])
            if len(output) != expected_size:
                raise ValueError("Wrong decompressed length")
            return bytes(output)
        if command >= 0xE0:
            literal = ((command & 31) << 2) + 4
        elif command >= 0xC0:
            a, b, c = data[cursor:cursor + 3]
            cursor += 3
            literal = command & 3
            back = ((command & 16) << 12) + (a << 8) + b + 1
            count = ((command & 12) << 6) + c + 5
        elif command >= 0x80:
            a, b = data[cursor:cursor + 2]
            cursor += 2
            literal = a >> 6
            back = ((a & 63) << 8) + b + 1
            count = (command & 63) + 4
        else:
            a = data[cursor]
            cursor += 1
            literal = command & 3
            back = ((command & 96) << 3) + a + 1
            count = ((command & 28) >> 2) + 3

        if cursor + literal > len(data):
            raise ValueError("Truncated RefPack literals")
        output.extend(data[cursor:cursor + literal])
        cursor += literal
        if back > len(output):
            raise ValueError("RefPack copy before start of output")
        start = len(output) - back
        for offset in range(count):
            output.append(output[start + offset])
        if len(output) > expected_size:
            raise ValueError("RefPack exceeded declared size")

    raise ValueError("Missing RefPack end marker")


def read(package, entry):
    with Path(package).open("rb") as stream:
        stream.seek(entry.offset)
        data = stream.read(entry.size)
    if len(data) != entry.size:
        raise ValueError(f"{package}: truncated {entry.key}")
    if entry.compression == 0 and entry.size == entry.uncompressed_size:
        return data
    try:
        if entry.compression == 0xFFFF:
            return refpack(data, entry.uncompressed_size)
        if entry.compression == 0x5A42:
            return zlib.decompress(data)
    except (IndexError, SystemError, zlib.error) as error:
        raise ValueError(f"{package}: corrupt {entry.key}: {error}") from error
    raise ValueError(f"{package}: unsupported compression {entry.compression:#x}")


def write(destination, resources):
    resources = list(resources)
    if len({key for key, _ in resources}) != len(resources):
        raise ValueError("Duplicate DBPF resource key")
    destination = Path(destination)
    header = bytearray(HEADER_SIZE)
    struct.pack_into("<4sII", header, 0, b"DBPF", 2, 0)
    index_size = 4 + 32 * len(resources)
    index_offset = HEADER_SIZE + sum(len(payload) for _, payload in resources)
    struct.pack_into("<III", header, 0x24, len(resources), 0, index_size)
    struct.pack_into("<II", header, 0x3C, 3, index_offset)

    with destination.open("wb") as stream:
        stream.write(header)
        entries = []
        for key, payload in resources:
            size = len(payload)
            entries.append((key, stream.tell(), size))
            stream.write(payload)
        stream.write(struct.pack("<I", 0))
        for key, offset, size in entries:
            stream.write(struct.pack("<IIIIIIIHH", key.type, key.group, key.instance >> 32,
                                     key.instance & 0xFFFFFFFF, offset, 0x80000000 | size,
                                     size, 0, 1))
