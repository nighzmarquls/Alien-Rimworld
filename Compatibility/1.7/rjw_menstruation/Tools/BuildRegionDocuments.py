"""Package existing textures into grouped OpenRaster documents beside Fetus.kra.
No source textures or existing art documents are modified. Explicit --overwrite is
required to rebuild the generated ORAs after they have been edited.
"""
from dataclasses import dataclass, field
from pathlib import Path
from io import BytesIO
import argparse
import json
import re
import xml.etree.ElementTree as ET
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED
from PIL import Image
from TextureValue import is_shared_overlay

compatibility = Path(__file__).resolve().parents[1]
repository = compatibility.parents[2]
art = compatibility / 'Textures/XMT/Menstruation'
upstream = repository.parent / 'rjw_menstruation/1.6/Textures'


@dataclass
class Node:
    name: str
    visible: bool = True
    path: Path | None = None
    children: list = field(default_factory=list)


def group(name, children, visible=True):
    return Node(name, visible, children=children)


def variants(name, paths, selected=None, visible=True):
    paths = sorted(paths)
    chosen = next((p for p in paths if p.stem == selected), paths[0] if paths else None)
    return group(name, [Node(p.name, p == chosen, p) for p in paths], visible)


def breasts(root):
    families = []
    for directory, label in [('Breasts', 'Natural'), ('Breasts_Artificial', 'Artificial'), ('Breasts_Udder', 'Udder')]:
        folder = root / directory
        sizes = []
        bases = sorted(p for p in folder.glob('*.png') if re.fullmatch(r'Breast_Breast\d\d', p.stem))
        for base in bases:
            sizes.append(group(f'Size {base.stem[-2:]}', [
                variants('Nipples - choose one', folder.glob(base.stem + '_Nipple*.png'), base.stem + '_Nipple01'),
                variants('Areolas - choose one', folder.glob(base.stem + '_Areola*.png'), base.stem + '_Areola01'),
                Node('Base - ' + base.name, path=base),
            ], base.stem.endswith('02')))
        legacy = sorted(p for p in folder.glob('*.png') if not p.stem.startswith('Breast_Breast'))
        if legacy:
            sizes.append(variants('Legacy composite alternatives', legacy, visible=False))
        families.append(group(label, sizes, directory == 'Breasts'))
    return families


def genitals(root):
    folder = root / 'Genitals'
    return [group('Natural', [
        variants('Vagina - choose size', folder.glob('Vagina*.png'), 'Vagina02'),
        variants('Anus - choose size', folder.glob('Anal*.png'), 'Anal02'),
    ]), group('Bionic', [
        variants('Vagina - choose size', folder.glob('BionicVagina*.png'), 'BionicVagina02'),
        variants('Anus - choose size', folder.glob('BionicAnal*.png'), 'BionicAnal02'),
    ], False)]


def womb(root):
    folder = root / 'Womb'
    base = sorted(p for p in folder.glob('*.png') if not is_shared_overlay(p.relative_to(root)))
    special_wombs = [variants(subfolder.relative_to(folder).as_posix(), subfolder.glob('*.png'))
                     for subfolder in sorted(folder.rglob('*')) if subfolder.is_dir() and any(subfolder.glob('*.png'))]
    fetus_groups = []
    fetus = root / 'Fetus'
    # Group stages by their exact filename prefix; keep multiplets separate from singletons.
    by_family = {}
    for path in sorted(fetus.rglob('*.png')):
        relative = path.relative_to(fetus)
        family = relative.parent.as_posix() if relative.parent != Path('.') else 'Default fetus'
        match = re.match(r'^(.*?)(\d\d)(_Multiplet_\d+)?$', path.stem)
        series = ((match[1].rstrip('_') or 'Horror') + (match[3] or '')) if match else path.stem
        by_family.setdefault(family, {}).setdefault(series, []).append(path)
    for family, series in by_family.items():
        fetus_groups.append(group(family, [variants(name, paths, visible=index == 0)
            for index, (name, paths) in enumerate(series.items())], False))
    return [group('Special womb alternatives - insect eggs', special_wombs, False),
            group('Fetus alternatives - replace the womb base', fetus_groups, False),
            variants('Womb and implanted alternatives - choose one', base, 'Womb')]


def eggs(root):
    return [variants('Egg development - choose stage', (root / 'Eggs').glob('*.png'), 'Egg', False),
            variants('Ovaries - choose stage', (root / 'Ovaries').glob('*.png'), 'Ovary_01')]


def png_bytes(image):
    stream = BytesIO()
    image.save(stream, format='PNG')
    return stream.getvalue()


def all_paths(nodes):
    for node in nodes:
        if node.path:
            yield node.path
        yield from all_paths(node.children)


def mask_nodes(nodes):
    """Mirror source associations in a separate hidden mask tree, without renaming art layers."""
    result = []
    for node in nodes:
        if node.path:
            relative = node.path.relative_to(art / 'Cryptimorph')
            path = art / 'Masks/Cryptimorph' / relative
            if path.exists():
                result.append(Node(node.name, node.visible, path))
        else:
            children = mask_nodes(node.children)
            if children:
                result.append(group(node.name, children, node.visible))
    return result


def render(nodes, size):
    result = Image.new('RGBA', size)
    # OpenRaster stores the topmost layer first, while compositing starts at the bottom.
    for node in reversed(nodes):
        if not node.visible:
            continue
        if node.path:
            with Image.open(node.path) as image:
                pixels = image.convert('RGBA')
            result.alpha_composite(pixels, ((size[0] - pixels.width) // 2, (size[1] - pixels.height) // 2))
        else:
            result = Image.alpha_composite(result, render(node.children, size))
    return result


def write_document(name, nodes, size, overwrite):
    target = art / (name + '.ora')
    if target.exists() and not overwrite:
        raise SystemExit(f'Refusing to replace {target.name}. Use --overwrite only to discard document edits.')
    root = ET.Element('image', {'version': '0.0.6', 'w': str(size[0]), 'h': str(size[1]), 'name': name,
                               'xres': '72', 'yres': '72'})
    stack = ET.SubElement(root, 'stack')
    mapping = []
    with ZipFile(target, 'w', compression=ZIP_DEFLATED) as archive:
        archive.writestr('mimetype', 'image/openraster', compress_type=ZIP_STORED)

        def add(parent, children):
            for node in children:
                attrs = {'name': node.name, 'visibility': 'visible' if node.visible else 'hidden',
                         'opacity': '1.0', 'composite-op': 'svg:src-over'}
                if node.path:
                    with Image.open(node.path) as image:
                        width, height = image.size
                    source = f'data/layer{len(mapping):04}.png'
                    attrs.update(src=source, x=str((size[0] - width) // 2), y=str((size[1] - height) // 2))
                    ET.SubElement(parent, 'layer', attrs)
                    archive.writestr(source, node.path.read_bytes())
                    mapping.append({'layer': source, 'source': str(node.path.relative_to(repository.parent)).replace('\\', '/')})
                else:
                    attrs['isolation'] = 'isolate'
                    add(ET.SubElement(parent, 'stack', attrs), node.children)

        add(stack, nodes)
        ET.indent(root)
        archive.writestr('stack.xml', ET.tostring(root, encoding='utf-8', xml_declaration=True))
        merged = render(nodes, size)
        archive.writestr('mergedimage.png', png_bytes(merged))
        thumbnail = merged.copy()
        thumbnail.thumbnail((256, 256))
        archive.writestr('Thumbnails/thumbnail.png', png_bytes(thumbnail))
        archive.writestr('source-map.json', json.dumps(mapping, indent=2))
    # Validate archive structure, PNG integrity, exact source bytes and native dimensions.
    with ZipFile(target) as archive:
        assert archive.testzip() is None
        assert archive.infolist()[0].filename == 'mimetype'
        assert archive.infolist()[0].compress_type == ZIP_STORED
        assert archive.read('mimetype') == b'image/openraster'
        layers = ET.fromstring(archive.read('stack.xml')).findall('.//layer')
        assert len(layers) == len(mapping)
        for layer, item in zip(layers, mapping):
            assert layer.attrib['src'] == item['layer']
            source = repository.parent / item['source']
            assert archive.read(item['layer']) == source.read_bytes()
            with Image.open(BytesIO(archive.read(item['layer']))) as image:
                image.verify()
        with Image.open(BytesIO(archive.read('mergedimage.png'))) as preview:
            assert preview.size == size and preview.convert('RGBA').tobytes() == merged.tobytes()
    print(f'{target.name}: {len(mapping)} layers, {len(root.findall(".//stack")) - 1} groups, {size[0]}x{size[1]}')
    return target, merged


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--overwrite', action='store_true')
    args = parser.parse_args()
    documents = [('Breasts', breasts, (102, 100)), ('Genitals', genitals, (102, 140)),
                 ('Womb_and_Fetus', womb, (256, 256)), ('Egg_and_Ovary', eggs, (317, 318))]
    # Check all destinations first so a previous edited document prevents partial replacement.
    if not args.overwrite and any((art / (name + '.ora')).exists() for name, _, _ in documents):
        raise SystemExit('ORA documents already exist. Use --overwrite only to discard their edits.')
    captured = set()
    previews = []
    for name, builder, size in documents:
        artwork = builder(art / 'Cryptimorph')
        nodes = [group('Cryptimorph shine masks - export individually', mask_nodes(artwork), False),
                 group('Cryptimorph - current runtime artwork', artwork),
                 group('Upstream reference - original colors', builder(upstream), False)]
        if name == 'Womb_and_Fetus':
            nodes.append(group('Default horror artwork', womb(art / 'Default'), False))
        captured.update(all_paths(nodes))
        target, merged = write_document(name, nodes, size, args.overwrite)
        previews.append((name, merged))
    expected = (set((art / 'Cryptimorph').rglob('*.png')) | set((art / 'Default').rglob('*.png')) |
                set((art / 'Masks/Cryptimorph').rglob('*.png')))
    assert expected <= captured, f'Unassigned local textures: {expected - captured}'
    # QA contact sheet stays outside the runtime texture tree.
    from PIL import ImageDraw
    sheet = Image.new('RGB', (1000, 265), '#363b40')
    draw = ImageDraw.Draw(sheet)
    for index, (name, preview) in enumerate(previews):
        preview.thumbnail((180, 220))
        sheet.paste(preview, (index * 200 + (200 - preview.width) // 2, 30), preview)
        draw.text((index * 200 + 8, 8), name, fill='white')
    checks = compatibility / 'Source/obj/RegionDocumentChecks'
    checks.mkdir(parents=True, exist_ok=True)
    sheet.save(checks / 'preview.png')
    print(f'All {len(expected)} local PNGs included, source bytes preserved. Fetus.kra untouched.')


if __name__ == '__main__':
    main()
