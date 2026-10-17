"""Encode rendered application frames; Pillow is documentation tooling only."""
from pathlib import Path
import sys
from PIL import Image


def encode(folder, output, duration):
    paths = sorted(folder.glob("*.png"))
    if not paths:
        raise ValueError(f"No rendered frames in {folder.name}")
    sample = Image.new("RGB", (160 * 4, 100), "#173b3c")
    for index, offset in enumerate([0, len(paths) // 3, 2 * len(paths) // 3, len(paths) - 1]):
        with Image.open(paths[offset]) as image:
            image = image.convert("RGB")
            image.thumbnail((160, 100))
            sample.paste(image, (index * 160, 0))
    palette = sample.quantize(colors=256)
    frames = []
    for path in paths:
        with Image.open(path) as image:
            frames.append(image.convert("RGB").quantize(palette=palette, dither=Image.Dither.NONE))
    delays = [((i + 1) * duration // 10 - i * duration // 10) * 10 for i in range(len(frames))]
    frames[0].save(output, save_all=True, append_images=frames[1:], duration=delays,
                   loop=0, optimize=True, disposal=1)
    print(f"{output.name}: {len(frames)} frames, {output.stat().st_size:,} bytes")


if __name__ == "__main__":
    source, target = map(Path, sys.argv[1:3])
    encode(source / "overview", target / "overview.gif", 125)
    encode(source / "pet", target / "pet-loop.gif", 80)
