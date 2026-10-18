"""Make white/transparent placeholder highlights; never modify base textures.
Select each texture's highest nontransparent HSV value (max RGB). Erode each
8-connected plateau by 5% of its shorter span on each side (~10% total shrink).
Subpixel erosion rounds to zero; thin regions may disappear. Alpha is retained.
Existing masks are preserved unless --overwrite is explicitly requested.
"""
from pathlib import Path
from collections import deque
import argparse
from PIL import Image, ImageFilter
from TextureValue import is_shared_overlay


def make_mask(image, shrink=0.10):
    image = image.convert('RGBA')
    width, height = image.size
    pixels = list(image.getdata())
    maximum = max((max(pixel[:3]) for pixel in pixels if pixel[3]), default=0)
    remaining = {index for index, pixel in enumerate(pixels) if maximum > 0 and pixel[3] and max(pixel[:3]) == maximum}
    keep = set()
    while remaining:
        seed = remaining.pop()
        component = {seed}
        pending = deque([seed])
        while pending:
            index = pending.popleft()
            x, y = index % width, index // width
            for yy in range(max(0, y - 1), min(height, y + 2)):
                for xx in range(max(0, x - 1), min(width, x + 2)):
                    neighbor = yy * width + xx
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        component.add(neighbor)
                        pending.append(neighbor)
        xs = [index % width for index in component]
        ys = [index // width for index in component]
        left, top, right, bottom = min(xs), min(ys), max(xs) + 1, max(ys) + 1
        radius = int(min(right - left, bottom - top) * shrink / 2 + 0.5)
        if radius == 0:
            keep.update(component)
            continue
        # Padding makes erosion at the image boundary behave like any other edge.
        local = Image.new('L', (right - left + 2 * radius, bottom - top + 2 * radius))
        for index in component:
            local.putpixel((index % width - left + radius, index // width - top + radius), 255)
        local = local.filter(ImageFilter.MinFilter(radius * 2 + 1))
        for index in component:
            if local.getpixel((index % width - left + radius, index // width - top + radius)):
                keep.add(index)
    result = Image.new('RGBA', image.size, (255, 255, 255, 0))
    result.putalpha(Image.frombytes('L', image.size, bytes(pixels[index][3] if index in keep else 0 for index in range(len(pixels)))))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--overwrite', action='store_true', help='Discard manual mask edits and regenerate.')
    args = parser.parse_args()
    art = Path(__file__).resolve().parents[1] / 'Textures/XMT/Menstruation'
    source = art / 'Cryptimorph'
    count = nonempty = 0
    for path in sorted(source.rglob('*.png')):
        relative = path.relative_to(source)
        if any(part.startswith('~') for part in relative.parts) or is_shared_overlay(relative):
            continue
        target = art / 'Masks/Cryptimorph' / relative
        if target.exists() and not args.overwrite:
            continue
        with Image.open(path) as image:
            mask = make_mask(image)
        target.parent.mkdir(parents=True, exist_ok=True)
        mask.save(target)
        count += 1
        nonempty += mask.getchannel('A').getbbox() is not None
    print(f'Created {count} masks ({nonempty} nonempty); base textures unchanged.')


if __name__ == '__main__':
    main()
