"""Apply a value lift to existing cryptimorph skin surfaces without regenerating user artwork.
This edits the current pixels; rerunning compounds the adjustment.
"""
from pathlib import Path
import argparse
from PIL import Image
from TextureValue import brighten, is_skin_surface

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--gamma', type=float, default=0.75)
args = parser.parse_args()
if not 0 < args.gamma < 1:
    raise SystemExit('Use a gamma between zero and one for a value lift.')
root = Path(__file__).resolve().parents[1] / 'Textures/XMT/Menstruation/Cryptimorph'
count = 0
for path in sorted(root.rglob('*.png')):
    if not is_skin_surface(path.relative_to(root)):
        continue
    with Image.open(path) as source:
        original = source.convert('RGBA')
    result = brighten(original, args.gamma)
    result.save(path)
    with Image.open(path) as saved:
        assert saved.size == original.size
        assert saved.getchannel('A').tobytes() == original.getchannel('A').tobytes()
        assert saved.convert('RGB').tobytes() == result.convert('RGB').tobytes()
    count += 1
print(f'Brightened {count} skin surfaces with gamma {args.gamma}; dimensions and alpha preserved.')
