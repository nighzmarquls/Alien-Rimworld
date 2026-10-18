"""Value correction for skin-tinted UI surfaces, excluding independently tinted overlays."""
from pathlib import Path
from PIL import Image


def is_shared_overlay(relative: Path) -> bool:
    return relative.parts[0] == 'Womb' and (
        relative.name == 'Empty.png' or relative.name.startswith('Womb_Cum_'))


def is_skin_surface(relative: Path) -> bool:
    return relative.parts[0] == 'Genitals' or (
        relative.parts[0] in {'Breasts', 'Breasts_Artificial', 'Breasts_Udder'}
        and relative.stem.startswith('Breast_Breast')
        and '_Nipple' not in relative.stem and '_Areola' not in relative.stem
    )


def brighten(image: Image.Image, gamma: float = 0.75) -> Image.Image:
    """Lift midtones (~19% at 128) while retaining black outlines, white highlights and alpha."""
    rgba = image.convert('RGBA')
    table = [round(255 * (value / 255) ** gamma) for value in range(256)]
    red, green, blue, alpha = rgba.split()
    return Image.merge('RGBA', (red.point(table), green.point(table), blue.point(table), alpha))
