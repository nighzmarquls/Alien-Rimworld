"""Exporter contract checks using a minimal Krita API double, not a GUI integration test.
All PNG writes stay under Source/obj/KritaTextureExportChecks/.
"""
from pathlib import Path
from zipfile import ZipFile
from copy import deepcopy
from contextlib import redirect_stdout
from io import StringIO
import runpy
import sys
import tempfile
import types
import xml.etree.ElementTree as ET
from PIL import Image

repository = Path(__file__).resolve().parents[2]
art = repository / 'Compatibility/1.7/rjw_menstruation/Textures/XMT/Menstruation'
checks = repository / '1.7/Source/obj/KritaTextureExportChecks'
checks.mkdir(parents=True, exist_ok=True)
count = 0


def check(name, value):
    global count
    assert value, name
    count += 1
    print('PASS ' + name)


class Node:
    def __init__(self, name, managed=False, visible=True):
        self.label, self.managed, self.visible = name, managed, visible
        self.children = []

    def name(self):
        return self.label

    def childNodes(self):
        return self.children

    def setVisible(self, value):
        assert self.managed, 'Unrelated layer visibility was modified: ' + self.label
        self.visible = value

    def colorModel(self):
        return 'RGBA'

    def colorDepth(self):
        return 'U8'

    def projectionPixelData(self, x, y, width, height):
        # A manually painted gray mask with partial alpha; export should turn
        # intensity into alpha, independent of the document's ColorHue treatment.
        return bytes((128, 128, 128, 200)) * (width * height)


def tree(spec):
    root = Node('root')
    for entry in spec['textures']:
        parent = root
        for name in entry['nodes']:
            child = next((node for node in parent.children if node.name() == name), None)
            if child is None:
                child = Node(name, True, False)
                parent.children.append(child)
            parent = child
        parent.source = entry['output']
    root.children.append(Node('ColorHue - artist treatment', False, True))
    root.children.append(Node('Unrelated hidden reference', False, False))
    root.children[0].children.append(Node('Custom shading inside source group', False, True))
    return root


class Document:
    def __init__(self, spec, root):
        self.spec, self.root = spec, tree(spec)
        self.filename = str(root / (spec['document'] + '.kra'))
        self.closed, self.clone_result, self.exports = False, None, 0
        self.fail_export = None

    def fileName(self):
        return self.filename

    def width(self):
        return self.spec['size'][0]

    def height(self):
        return self.spec['size'][1]

    def rootNode(self):
        return self.root

    def clone(self):
        self.clone_result = deepcopy(self)
        return self.clone_result

    def waitForDone(self):
        pass

    def refreshProjection(self):
        pass

    def setBatchmode(self, value):
        assert value

    def close(self):
        self.closed = True

    def exportImage(self, destination, config):
        self.exports += 1
        if self.exports == self.fail_export:
            return False
        visible_sources = []

        def walk(node, visible=True):
            visible = visible and node.visible
            if visible and hasattr(node, 'source'):
                visible_sources.append(node.source)
            for child in node.children:
                walk(child, visible)

        walk(self.root)
        assert len(visible_sources) == 1, 'Source variants contaminated one another'
        assert self.root.children[-2].visible, 'Color treatment was disabled'
        assert config.values['alpha'] is True
        image = Image.new('RGBA', self.spec['size'], (10, 20, 30, 255))
        image.putpixel((0, 0), (0, 0, 0, 0))
        image.save(destination)
        return True


class InfoObject:
    def __init__(self):
        self.values = {}

    def setProperty(self, name, value):
        self.values[name] = value


class QImage:
    Format_ARGB32 = 5

    def __init__(self, source, *args):
        if args:
            width, height, stride, format = args
            self.image = Image.frombytes('RGBA', (width, height), source, 'raw', 'BGRA')
            return
        if isinstance(source, Image.Image):
            self.image = source.copy()
        else:
            with Image.open(source) as image:
                self.image = image.copy()

    def isNull(self):
        return False

    def width(self):
        return self.image.width

    def height(self):
        return self.image.height

    def hasAlphaChannel(self):
        return 'A' in self.image.getbands()

    def copy(self, *args):
        if not args:
            return QImage(self.image)
        x, y, width, height = args
        return QImage(self.image.crop((x, y, x + width, y + height)))

    def save(self, path, format):
        self.image.save(path, format=format)
        return True


sys.modules['krita'] = types.SimpleNamespace(InfoObject=InfoObject)
sys.modules['PyQt5'] = types.ModuleType('PyQt5')
sys.modules['PyQt5.QtGui'] = types.SimpleNamespace(QImage=QImage)

scripts = {}
for path in sorted(art.glob('Export_*.py')):
    namespace = runpy.run_path(str(path), run_name='users_script')
    spec = namespace['EXPORT_SPEC']
    scripts[spec['document']] = namespace
    # Resolve paths against actual current ORA/KRA files, including the user's edits.
    document_path = art / (spec['document'] + ('.kra' if (art / (spec['document'] + '.kra')).exists() else '.ora'))
    with ZipFile(document_path) as archive:
        xml = ET.fromstring(archive.read('maindoc.xml' if document_path.suffix == '.kra' else 'stack.xml'))
    paths = set()

    def gather(element, prefix=()):
        tag = element.tag.split('}')[-1]
        if tag in ('layer', 'stack') and element.get('name'):
            prefix += (element.get('name'),)
            paths.add(prefix)
        for child in element:
            gather(child, prefix)

    gather(xml)
    with ZipFile(art / (spec['document'] + '.ora')) if spec['document'] != 'Fetus' else ZipFile(document_path) as archive:
        mask_xml = ET.fromstring(archive.read('maindoc.xml' if spec['document'] == 'Fetus' else 'stack.xml'))
    saved_paths = paths.copy()
    paths.clear()
    gather(mask_xml)
    ora_paths = paths.copy()
    check(spec['document'] + ' source paths match saved artwork and mask ORA groups', all(
        tuple(entry['nodes']) in (ora_paths if entry.get('kind') == 'mask' else saved_paths) for entry in spec['textures']))
    check(spec['document'] + ' has saved-Scripter main entry point', callable(namespace.get('main')))

with tempfile.TemporaryDirectory(dir=checks) as directory:
    root = Path(directory)
    (root / 'Cryptimorph').mkdir()
    (root / 'Default').mkdir()
    namespace = scripts['Egg_and_Ovary']
    spec = namespace['EXPORT_SPEC']
    document = Document(spec, root)
    before = deepcopy(document.root.__dict__)
    for entry in spec['textures']:
        target = root / entry['output']
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b'previous texture')
    exported = namespace['export_document'](document, spec, ['Cryptimorph/Eggs/*'], ['*/Egg_Fertilized00.png'])
    check('include/exclude exports seven egg textures and their seven masks', exported == 14)
    check('excluded textures retain their previous bytes', (root / 'Cryptimorph/Eggs/Egg_Fertilized00.png').read_bytes() == b'previous texture')
    check('other regions retain their previous bytes', (root / 'Cryptimorph/Ovaries/Ovary_01.png').read_bytes() == b'previous texture')
    with Image.open(root / 'Cryptimorph/Eggs/Egg.png') as image:
        check('color treatment is included and transparency retained', image.getpixel((1, 1)) == (10, 20, 30, 255) and image.getpixel((0, 0))[3] == 0)
    with Image.open(root / 'Masks/Cryptimorph/Eggs/Egg.png') as image:
        check('mask export bypasses global treatment and folds gray into alpha', image.getpixel((1, 1)) == (255, 255, 255, 100))
    check('hidden clone closes after success', document.clone_result.closed)

    def signature(node):
        return node.label, node.visible, [signature(child) for child in node.children]

    pristine = Document(spec, root)
    check('original and all unrelated visibility states remain unchanged', signature(document.root) == signature(pristine.root))
    failing = Document(spec, root)
    failing.fail_export = 2
    originals = {entry['output']: (root / entry['output']).read_bytes() for entry in spec['textures']}
    try:
        namespace['export_document'](failing, spec, ['*'], [])
        raise AssertionError('Expected staging failure')
    except RuntimeError as error:
        check('a failed render leaves every destination unchanged', all((root / path).read_bytes() == data for path, data in originals.items()))
        check('hidden clone closes after failure', failing.clone_result.closed)
    missing = Document(spec, root)
    missing.root.children[0].children[0].children.pop()
    try:
        namespace['export_document'](missing, spec, ['*'], [])
        raise AssertionError('Expected missing layer error')
    except RuntimeError:
        check('missing source layer fails before cloning or writing', missing.clone_result is None)
    sys.modules['krita'].Krita = types.SimpleNamespace(instance=lambda: types.SimpleNamespace(activeDocument=lambda: document))
    namespace['main'].__globals__['DRY_RUN'] = True
    with redirect_stdout(StringIO()):
        result = namespace['main']()
    check('loaded Scripter main validates art and mask destinations', result == 22)
    old_kra = Document(spec, root)
    old_kra.root.children = [node for node in old_kra.root.children if not node.name().startswith('Cryptimorph shine masks')]
    old_count = namespace['export_document'](old_kra, spec, ['*'], [], dry_run=True)
    check('older KRAs without masks still export all eleven art variants', old_count == 11)
    womb = scripts['Womb_and_Fetus']
    check('womb export excludes shared fluids and their masks', all(
        Path(entry['output']).name != 'Empty.png' and not Path(entry['output']).name.startswith('Womb_Cum_')
        for entry in womb['EXPORT_SPEC']['textures']))

    first, second = root / 'one.png', root / 'two.png'
    first.write_bytes(b'old one')
    second.write_bytes(b'old two')
    staged = root / 'stage.png'
    staged.write_bytes(b'new')
    commit_globals = namespace['_commit'].__globals__
    original_write = commit_globals['_atomic_write']
    calls = 0

    def fail_once(target, payload):
        global calls
        calls += 1
        if calls == 2:
            raise OSError('Simulated disk write failure')
        return original_write(target, payload)

    commit_globals['_atomic_write'] = fail_once
    try:
        namespace['_commit']([(first, staged), (second, staged)])
        raise AssertionError('Expected commit failure')
    except OSError:
        check('partial commit restores all previous destination bytes', first.read_bytes() == b'old one' and second.read_bytes() == b'old two')
    finally:
        commit_globals['_atomic_write'] = original_write

print('ALL %d KRITA EXPORT CHECKS PASSED (API double; live Krita rendering not tested)' % count)
