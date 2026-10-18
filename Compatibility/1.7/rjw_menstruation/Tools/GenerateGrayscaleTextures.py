"""Deterministic, alpha-preserving copies of upstream anatomy textures; never edits the originals."""
from pathlib import Path
import argparse
import xml.etree.ElementTree as ET
from PIL import Image, ImageOps
from TextureValue import brighten, is_skin_surface, is_shared_overlay

compatibility = Path(__file__).resolve().parents[1]
repository = compatibility.parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, default=repository.parent / 'rjw_menstruation/1.6/Textures')
parser.add_argument('--overwrite', action='store_true', help='Explicitly regenerate copies; this discards any later art treatment.')
args = parser.parse_args()
folders = {'Breasts', 'Breasts_Artificial', 'Breasts_Udder', 'Eggs', 'Fetus', 'Genitals', 'Ovaries', 'Womb'}
sources = sorted(p for p in args.source.rglob('*.png')
                 if p.relative_to(args.source).parts[0] in folders
                 and not is_shared_overlay(p.relative_to(args.source))
                 and not any(part.startswith('~') for part in p.relative_to(args.source).parts))
prefix = 'XMT/Menstruation/Cryptimorph/'
output = compatibility / 'Textures' / prefix
if not sources:
    raise SystemExit('No upstream anatomy PNGs found.')
if not args.overwrite and any((output / p.relative_to(args.source)).exists() for p in sources):
    raise SystemExit('Copies already exist. Use --overwrite only if you intend to discard subsequent art edits.')
for source in sources:
    relative = source.relative_to(args.source)
    destination = output / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(source) as image:
        rgba = image.convert('RGBA')
        gray = ImageOps.grayscale(rgba)
        result = Image.merge('RGBA', (gray, gray, gray, rgba.getchannel('A')))
        if is_skin_surface(relative):
            result = brighten(result)
        result.save(destination)
        with Image.open(destination) as saved:
            assert saved.size == rgba.size
            assert saved.getchannel('A').tobytes() == rgba.getchannel('A').tobytes()
            red, green, blue, _ = saved.split()
            assert red.tobytes() == green.tobytes() == blue.tobytes()

root = ET.Element('Defs')
definition = ET.SubElement(root, 'XMT_Menstruation.CryptimorphTextureSetDef')
ET.SubElement(definition, 'defName').text = 'XMT_CryptimorphTextures'
ET.SubElement(definition, 'prefix').text = prefix
ET.SubElement(definition, 'defaultPrefix').text = 'XMT/Menstruation/Default/'
ET.SubElement(definition, 'shineMaskPrefix').text = 'XMT/Menstruation/Masks/Cryptimorph/'
ET.SubElement(definition, 'shineOpacity').text = '0.65'
genes = ET.SubElement(definition, 'derivedGenes')
for gene in ET.parse(repository / '1.7/Defs/GeneDefs/GeneDefs_HybridGenes.xml').findall('.//Xenomorphtype.XMT_GeneSetDef/genes/li'):
    ET.SubElement(genes, 'li').text = gene.text
paths = ET.SubElement(definition, 'sourcePaths')
for stage in range(6):
    ET.SubElement(paths, 'li').text = f'Fetus/Horror/{stage:02}'
for source in sources:
    ET.SubElement(paths, 'li').text = source.relative_to(args.source).with_suffix('').as_posix()
ET.indent(root, space='  ')
manifest = compatibility / 'Defs/CryptimorphTextures.xml'
manifest.parent.mkdir(parents=True, exist_ok=True)
manifest.write_text('<?xml version="1.0" encoding="utf-8"?>\n' + ET.tostring(root, encoding='unicode') + '\n', encoding='utf-8')
print(f'Created and verified {len(sources)} grayscale textures. Dimensions and alpha preserved for every file.')
