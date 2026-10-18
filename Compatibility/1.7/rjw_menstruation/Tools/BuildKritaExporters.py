"""Generate self-contained Krita Scripter scripts; never rewrites ORA/KRA documents."""
from pathlib import Path
from pprint import pformat
from PIL import Image
import BuildRegionDocuments as regions

tools = Path(__file__).resolve().parent
art = regions.art
runtime = (tools / 'KritaExportRuntime.py').read_text(encoding='utf-8')


def entries(nodes, canvas, prefix=()):
    for node in nodes:
        chain = prefix + (node.name,)
        if node.path:
            if art not in node.path.parents or any(part.startswith('~') for part in node.path.relative_to(art).parts):
                continue
            with Image.open(node.path) as image:
                width, height = image.size
            yield {'nodes': list(chain), 'output': node.path.relative_to(art).as_posix(),
                   'rect': [(canvas[0] - width) // 2, (canvas[1] - height) // 2, width, height]}
        yield from entries(node.children, canvas, chain)


def write(spec):
    path = art / ('Export_' + spec['document'] + '.py')
    includes = ['Cryptimorph/Breasts*/*[Nn]ip*.png', 'Cryptimorph/Breasts*/*[Aa]reola*.png'] if spec['document'] == 'Breasts' else ['Cryptimorph/*']
    header = '''# Run inside Krita: Tools > Scripts > Scripter > Open this file > Run.
# Uses the active document's live, unsaved artwork; no additional Python packages.
# Color-treatment/filter layers are retained. Only source PNG variants are isolated
# in a hidden clone; the original document is never changed.
# Outputs overwrite matching local runtime PNGs after all renders succeed.

# Match output paths, e.g. ["Cryptimorph/*"] or ["Cryptimorph/Breasts/*"].
INCLUDE_PATTERNS = __DEFAULT_INCLUDES__
# Example: ["*/Empty.png", "*/Fetus/*"]
EXCLUDE_PATTERNS = []
# Export each selected texture's associated mask too, when its layer group exists.
EXPORT_SHINE_MASKS = True
# Leave None to export beside the source document; use an absolute folder if moved.
OUTPUT_ROOT = None
# True lists destinations and validates layer names without writing PNGs.
DRY_RUN = False

'''
    header = header.replace('__DEFAULT_INCLUDES__', repr(includes))
    # Installed Krita 5.2 imports saved Scripter files and then calls main();
    # unsaved editor text instead executes with __name__ == '__main__'. Support both.
    path.write_text(header + runtime + '\n\nEXPORT_SPEC = ' + pformat(spec, width=115, sort_dicts=False) +
                    '\n\ndef main():\n    return run_export(EXPORT_SPEC)\n' +
                    '\n\nif __name__ == "__main__":\n    main()\n', encoding='utf-8')
    print(path.name + ': ' + str(len(spec['textures'])) + ' source textures')


for name, builder, size in [('Breasts', regions.breasts, (102, 100)),
                            ('Genitals', regions.genitals, (102, 140)),
                            ('Womb_and_Fetus', regions.womb, (256, 256)),
                            ('Egg_and_Ovary', regions.eggs, (317, 318))]:
    artwork = builder(art / 'Cryptimorph')
    nodes = [regions.group('Cryptimorph - current runtime artwork', artwork)]
    if name == 'Womb_and_Fetus':
        nodes.append(regions.group('Default horror artwork', regions.womb(art / 'Default')))
    textures = list(entries(nodes, size))
    masks = list(entries([regions.group('Cryptimorph shine masks - export individually', regions.mask_nodes(artwork))], size))
    for entry in masks:
        entry['kind'] = 'mask'
        entry['associated_texture'] = entry['output'].removeprefix('Masks/')
    write({'document': name, 'size': list(size), 'textures': textures + masks})

# The earlier hand-authored Fetus.kra uses a simpler hierarchy. Its Background
# and any added treatment layers are intentionally not controlled by this script.
write({'document': 'Fetus', 'size': [256, 256], 'textures': [
    {'nodes': ['Womb', '%02d.png' % stage], 'output': 'Cryptimorph/Fetus/Horror/%02d.png' % stage,
     'rect': [0, 0, 256, 256]} for stage in range(6)]})
