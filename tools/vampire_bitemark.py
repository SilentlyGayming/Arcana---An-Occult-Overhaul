import argparse
import shutil
import struct
import subprocess
from pathlib import Path

IMG=0x00B2D882
THUMB=0x626F60CE
THUMB_INSTANCE=0x08146A9FB7FA3A0A
THUMB_GROUPS=(1, 2, 3, 4)
REPLACE={
  (IMG, 0, 0x9C4DC2ABCC9D8BFE): 'BiteMark_Multiplier.dds',
  (IMG, 0, 0x9C4DC2ABCC9D8BE0): 'BiteMark_Specular.dds',
}

DEFAULT_ASSETS = (Path(__file__).resolve().parent.parent / "assets" / "Textures"
                  / "Vampires" / "BiteMark")
DEFAULT_THUMBNAILS = (Path(__file__).resolve().parent.parent / "assets" / "Thumbnails"
                      / "Vampires" / "BiteMark")


def check_dds(blob: bytes, fmt: bytes, name: str):
    if len(blob)<128 or blob[:4]!=b'DDS ':
        raise ValueError(f'{name}: not a DDS')
    w=struct.unpack_from('<I',blob,16)[0]
    h=struct.unpack_from('<I',blob,12)[0]
    mip=struct.unpack_from('<I',blob,28)[0]
    fourcc=blob[84:88]
    if (w,h,mip,fourcc)!=(1024,1024,11,fmt):
        raise ValueError(f'{name}: expected 1024x1024, 11 mips, {fmt!r}; got {w}x{h}, {mip} mips, {fourcc!r}')
    if len(blob)!=(1398256 if fmt==b'DXT5' else 699192):
        raise ValueError(f'{name}: unexpected DDS length {len(blob)}')


def replacement_payloads(assets: Path = None):
    # Loads and validates the two DDS bite-mark replacements.
    directory = Path(assets) if assets is not None else DEFAULT_ASSETS
    resources = []
    for (typ, group, instance), filename in REPLACE.items():
        path = directory / filename
        if not path.is_file():
            raise FileNotFoundError(
                f"Vampire bite-mark texture not found: {path}\n"
                "Keep both DDS files in assets/Textures/Vampires/BiteMark/."
            )
        data = path.read_bytes()
        check_dds(data, b'DXT5' if 'Multiplier' in filename else b'DXT1', filename)
        resources.append((typ, group, instance, data))
    return resources


def thumbnail_payloads(assets: Path = None):
    # Loads the four unedited 256px CAS thumbnails rendered by TS3.
    directory=Path(assets) if assets is not None else DEFAULT_THUMBNAILS
    resources=[]
    for group in THUMB_GROUPS:
        filename=f'VampireBirthMark_256_Group_{group:08X}.png'
        path=directory / filename
        if not path.is_file():
            raise FileNotFoundError(
                f"Vampire birth-mark CAS thumbnail not found: {path}\n"
                "Keep all four PNGs in assets/Thumbnails/Vampires/BiteMark/."
            )
        data=path.read_bytes()
        if (len(data) < 33 or data[:8] != b'\x89PNG\r\n\x1a\n'
                or data[12:16] != b'IHDR'
                or struct.unpack_from('>II', data, 16) != (256,256)):
            raise ValueError(f'{filename}: expected an unmodified 256x256 PNG')
        resources.append((THUMB, group, THUMB_INSTANCE, data))
    return resources


def main():
    root=Path(__file__).resolve().parent.parent
    p=argparse.ArgumentParser(description="Regenerate the bite-mark textures from the extracted TS4 source.")
    p.add_argument('--source', type=Path, default=root/'source'/'TS4_GP04_VampireBite_Working_Cleaned.png')
    p.add_argument('--output-dir',type=Path,default=DEFAULT_ASSETS)
    p.add_argument('--opacity',type=float,default=1.5,help='Alpha multiplier. Original source max=161; default clamps near 242.')
    p.add_argument('--png-only',action='store_true',help='Do not encode DDS; use an image editor to export DXT5/DXT1 later.')
    args=p.parse_args()
    if not 0.1<=args.opacity<=3:
        p.error('--opacity must be between 0.1 and 3')
    args.output_dir.mkdir(parents=True,exist_ok=True)
    from PIL import Image, ImageChops
    source=Image.open(args.source).convert('RGBA')
    if source.size!=(1024,2048):
        p.error(f'Expected 1024x2048 TS4 source, got {source.size}')
    alpha_source=source.getchannel('A')
    mark=Image.new('L',(1024,1024),0)
    placements=[
        ((166,1030,176,1036),(11,9),(405,29)),
        ((169,1036,178,1043),(11,10),(408,42)),
    ]
    for crop_box,size,(cx,cy) in placements:
        stamp=alpha_source.crop(crop_box).resize(size,Image.Resampling.LANCZOS)
        stamp=stamp.point(lambda v:min(255,round(v*args.opacity)))
        layer=Image.new('L',(1024,1024),0)
        layer.paste(stamp,(cx-size[0]//2,cy-size[1]//2))
        mark=ImageChops.lighter(mark,layer)
    multiplier=Image.new('RGBA',(1024,1024),(148,148,148,0))
    multiplier.putalpha(mark)
    multiplier_png=args.output_dir/'BiteMark_Multiplier.png'
    multiplier.save(multiplier_png)
    highlight=mark.point(lambda v:round(v*70/255))
    specular=Image.merge('RGB',(highlight,highlight,highlight))
    specular_png=args.output_dir/'BiteMark_Specular.png'
    specular.save(specular_png)
    if args.png_only:
        print('PNG only: use DDS DXT5 for Multiplier and DXT1 for Specular, 1024x1024 with 11 mip levels.')
    else:
        if not shutil.which('magick'):
            raise SystemExit('ImageMagick magick not found on PATH. Rerun with --png-only and export DDS in Photoshop/GIMP.')
        for file,fmt in [(multiplier_png,'dxt5'),(specular_png,'dxt1')]:
            subprocess.run(['magick',str(file),'-define',f'dds:compression={fmt}',
                            '-define','dds:mipmaps=11',str(file.with_suffix('.dds'))],check=True)
        print('Created DXT5 and DXT1 DDS textures with complete mip chains')
    print('Wrote converted textures to:',args.output_dir.resolve())


if __name__ == '__main__':
    main()
