"""Pixel-level mask generation and runtime asset association checks; no game startup."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw

repository = Path(__file__).resolve().parents[2]
compatibility = repository / 'Compatibility/1.7/rjw_menstruation'
sys.path.insert(0, str(compatibility / 'Tools'))
from GenerateShineMasks import make_mask

sample = Image.new('RGBA', (100, 100), (255, 255, 255, 0))
ImageDraw.Draw(sample).rectangle((10, 10, 89, 89), fill=(200, 200, 200, 77))
mask = make_mask(sample)
assert mask.getchannel('A').getbbox() == (14, 14, 86, 86), '80px plateau should shrink to 72px (10%)'
assert mask.getpixel((50, 50)) == (255, 255, 255, 77), 'Original alpha must be retained'
assert mask.getpixel((10, 10))[3] == 0, 'Boundary must erode'
sample.putpixel((50, 50), (255, 255, 255, 77))
mask = make_mask(sample)
assert mask.getchannel('A').getbbox() == (50, 50, 51, 51), 'Only the maximum nontransparent value qualifies'
assert make_mask(Image.new('RGBA', (20, 20), (0, 0, 0, 255))).getchannel('A').getbbox() is None, 'Black has no highlights'
print('PASS: maximum-value selection, transparent pixels, alpha and ~10% erosion')

art = compatibility / 'Textures/XMT/Menstruation'
definition = ET.parse(compatibility / 'Defs/CryptimorphTextures.xml').getroot()[0]
count = 0
for source in sorted((art / 'Cryptimorph').rglob('*.png')):
    relative = source.relative_to(art / 'Cryptimorph')
    if any(part.startswith('~') for part in relative.parts):
        continue
    with Image.open(source) as base, Image.open(art / 'Masks/Cryptimorph' / relative) as mask:
        assert base.size == mask.size, str(relative)
        red, green, blue, alpha = mask.convert('RGBA').split()
        assert red.getextrema() == green.getextrema() == blue.getextrema() == (255, 255)
        assert all(m <= b for m, b in zip(alpha.tobytes(), base.convert('RGBA').getchannel('A').tobytes()))
    count += 1
assert definition.findtext('shineMaskPrefix') == 'XMT/Menstruation/Masks/Cryptimorph/'
print(f'PASS: all {count} cryptimorph masks preserve dimensions and stay within source alpha')
