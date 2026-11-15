"""Grade the existing wall bake; write 1K base colour, roughness and tangent normals."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

textures = Path(__file__).resolve().parents[1] / "SourceAssets/levels/textures/ModularStoneWall"
source = textures / "Stage21Bake"


def load(role):
    return np.asarray(Image.open(source / f"Runtime{role}.png").convert("RGB"), dtype=float)


def smooth(values, radius):
    image = Image.fromarray(np.uint8(np.clip(values, 0, 255)))
    return np.asarray(image.filter(ImageFilter.GaussianBlur(radius)), dtype=float)


def save(role, values):
    Image.fromarray(np.uint8(np.clip(np.rint(values), 0, 255))).save(textures / f"Runtime{role}.png")


luma = load("Color") @ [0.2126, 0.7152, 0.0722]
valid = luma > 8
# Normalize the blur by occupied texels to avoid pulling atlas gutters into stone.
def stone_blur(radius):
    weight = smooth(valid * 255, radius)
    return smooth(luma * valid, radius) * 255 / np.maximum(weight, 1)


value = .2 * luma + .65 * stone_blur(2) + .15 * stone_blur(7)
normal = load("Normal")
slope = np.clip((245 - smooth(normal[:, :, 2], 2)) / 65, 0, 1)
tone = np.clip((value - 43) / 28 - .16 * slope, 0, 1)
palette = np.array([[43,48,50], [77,84,87], [112,118,119], [139,143,141]])
color = np.stack([np.interp(tone,[0,.46,.85,1],channel) for channel in palette.T],axis=-1)
moss = .20 * np.clip((61 - stone_blur(12))/8,0,1) * (.3 + .7*slope)
color = color * (1-moss[:,:,None]) + np.array([67,75,60]) * moss[:,:,None]
save("Color",color)
rough = np.clip(.84 + .05*slope + .04*(smooth(load("Roughness")[:,:,0],2)-132)/32,.80,.92)
save("Roughness",np.repeat(rough[:,:,None]*255,3,axis=2))
filtered = np.stack([smooth(normal[:,:,i],1.6) for i in range(3)],axis=-1)
vectors = (.25*normal + .75*filtered)/127.5 - 1
vectors /= np.maximum(np.linalg.norm(vectors,axis=-1,keepdims=True),.001)
save("Normal",(vectors+1)*127.5)
