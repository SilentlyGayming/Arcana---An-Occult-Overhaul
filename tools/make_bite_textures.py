#!/usr/bin/env python3
"""Regenerate converted UV-aligned TS3 vampire mark DDS files from the independently extracted TS4 EA source.

Requirements: Pillow (py -3 -m pip install pillow), ImageMagick 'magick' (optional with --png-only).
This script deliberately uses only EA's user-extracted originals, not other creators' conversion assets.
"""
from __future__ import annotations
import argparse
from pathlib import Path
import shutil
import subprocess
from PIL import Image, ImageChops


def main():
    root=Path(__file__).resolve().parent.parent
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', type=Path, default=root/'source'/'TS4_GP04_VampireBite_Working_Cleaned.png')
    p.add_argument('--output-dir',type=Path,default=root/'assets'/'Textures'/'Vampire'/'BiteMark')
    p.add_argument('--opacity',type=float,default=1.5,help='Alpha multiplier. Original source max=161; default clamps near 242.')
    p.add_argument('--png-only',action='store_true',help='Do not encode DDS; use an image editor to export DXT5/DXT1 later.')
    args=p.parse_args()
    if not 0.1<=args.opacity<=3:
        p.error('--opacity must be between 0.1 and 3')
    args.output_dir.mkdir(parents=True,exist_ok=True)
    source=Image.open(args.source).convert('RGBA')
    if source.size!=(1024,2048):
        p.error(f'Expected 1024x2048 TS4 source, got {source.size}')
    alpha_source=source.getchannel('A')
    mark=Image.new('L',(1024,1024),0)
    # The original TS4 UV punctures are about x167..176 / y1031..1041.
    # Split into two tiny punctures so their individual proportions survive relocation.
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
    # Grayscale multiplier retains compatibility with the four original TS3 recolor presets.
    multiplier=Image.new('RGBA',(1024,1024),(148,148,148,0))
    multiplier.putalpha(mark)
    multiplier_png=args.output_dir/'BiteMark_Multiplier.png'
    multiplier.save(multiplier_png)
    # Preset original: DXT1 opaque black with a small grey specular highlight at mark position.
    # Reconstruct highlights in the same channels and position as the new punctures.
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

if __name__=='__main__':main()
