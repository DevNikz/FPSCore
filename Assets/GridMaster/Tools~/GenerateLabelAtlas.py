#!/usr/bin/env python3
"""
Generates GridMasterLabelAtlas.png: a signed-distance-field atlas of the glyphs used by the
GridMaster label feature (digits, minus, m, c, u, period).

Requires: pillow, numpy, scipy.   Usage: python GenerateLabelAtlas.py [font.ttf] [output.png]

The constants below MUST match the #defines at the top of Shaders/Includes/GridMasterLabels.hlsl.
Glyph order (index = column + row * COLUMNS, row 0 is the TOP row of the image):
  0-9 digits, 10 '-', 11 'm', 12 'c', 13 'u', 14 '.', 15 unused
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import distance_transform_edt

COLUMNS = 8
ROWS = 2
CELL = 64            # texels per glyph cell (square cell, stretched to CHAR_ASPECT in the shader)
SPREAD = 8           # SDF range in output texels (distance 0.5 +- SPREAD/ (2*SPREAD))
CHAR_ASPECT = 0.6    # character box width / height used by the shader
GLYPHS = "0123456789-mcu."

WORK = 256           # SDF is computed at WORK x WORK, then box-downsampled to CELL
SUPERSAMPLE = 2      # hi-res glyph canvas = WORK*SUPERSAMPLE tall
DEFAULT_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def render_glyph_mask(char, font_path):
    """Returns a WORK x WORK float mask (1 = inside) with the glyph squeezed into the character box."""
    hr_h = WORK * SUPERSAMPLE
    hr_w = int(round(hr_h * CHAR_ASPECT))
    # Digits are 62% of the cell height, baseline 22% above the bottom (room for SDF spread).
    digit_height = 0.62 * hr_h
    font_size = 10
    font = ImageFont.truetype(font_path, font_size)
    while True:
        bbox = font.getbbox("0", anchor="ls")
        if (bbox[3] - bbox[1]) >= digit_height:
            break
        font_size += 2
        font = ImageFont.truetype(font_path, font_size)

    canvas = Image.new("L", (hr_w * 3, hr_h), 0)  # wide canvas so wide glyphs are not clipped
    draw = ImageDraw.Draw(canvas)
    baseline = int(0.78 * hr_h)
    draw.text((hr_w * 1.5, baseline), char, font=font, fill=255, anchor="ms")

    arr = np.asarray(canvas, dtype=np.float32) / 255.0
    cols = np.where(arr.max(axis=0) > 0.5)[0]
    if cols.size == 0:
        return np.zeros((WORK, WORK), dtype=np.float32)
    left, right = cols.min(), cols.max() + 1
    glyph = canvas.crop((left, 0, right, hr_h))

    # Keep SPREAD worth of margin on both sides horizontally; squeeze wide glyphs ('m') to fit.
    max_width = int(hr_w * (1.0 - 2.0 * SPREAD / CELL))
    if glyph.width > max_width:
        glyph = glyph.resize((max_width, hr_h), Image.LANCZOS)

    box = Image.new("L", (hr_w, hr_h), 0)
    box.paste(glyph, ((hr_w - glyph.width) // 2, 0))
    # Stretch the character box to the square cell (the shader squeezes it back to CHAR_ASPECT).
    box = box.resize((WORK, WORK), Image.LANCZOS)
    return (np.asarray(box, dtype=np.float32) / 255.0 > 0.5).astype(np.float32)

def mask_to_sdf_cell(mask):
    inside = mask > 0.5
    dist_out = distance_transform_edt(~inside)
    dist_in = distance_transform_edt(inside)
    signed = dist_in - dist_out                      # positive inside, in WORK texels
    work_spread = SPREAD * (WORK / CELL)
    sdf = np.clip(0.5 + signed / (2.0 * work_spread), 0.0, 1.0)
    factor = WORK // CELL
    sdf = sdf.reshape(CELL, factor, CELL, factor).mean(axis=(1, 3))   # box downsample
    return sdf

def main():
    font_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_FONT
    out_path = sys.argv[2] if len(sys.argv) > 2 else "GridMasterLabelAtlas.png"
    atlas = np.zeros((ROWS * CELL, COLUMNS * CELL), dtype=np.float32)   # unused cells = 0 (outside)
    for index, char in enumerate(GLYPHS):
        row, col = divmod(index, COLUMNS)
        cell = mask_to_sdf_cell(render_glyph_mask(char, font_path))
        atlas[row * CELL:(row + 1) * CELL, col * CELL:(col + 1) * CELL] = cell
    Image.fromarray((atlas * 255.0 + 0.5).astype(np.uint8), mode="L").save(out_path)
    print("Wrote", out_path, atlas.shape[::-1])

if __name__ == "__main__":
    main()
