"""Copy the six selected original Blender character scenes into runtime GLBs.

Run with Blender --background --factory-startup --python this_file -- [--inspect].
Never saves a .blend, changes weights, creates geometry, or generates animations.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "Docs/MOBA_BETA_MODEL_SOURCES.json"
entries = json.loads(MANIFEST.read_text(encoding="utf-8-sig"))
inspect_only = "--inspect" in sys.argv

for entry in entries:
    source = ROOT / entry["OriginalBlenderSource"]
    digest = hashlib.sha256(source.read_bytes()).hexdigest().upper()
    if digest != entry["OriginalBlenderSHA256"]:
        raise RuntimeError(f"Source changed since manifest: {source}")
    with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
        print(f"BETA SOURCE {entry['Model']}: scenes={list(available.scenes)}", flush=True)
        if inspect_only:
            continue
        scene_name = entry.get("MainScene", "Scene")
        if scene_name not in available.scenes:
            raise RuntimeError(f"Main Scene needs explicit selection: {source}")
        loaded.scenes = [scene_name]
    scene = loaded.scenes[0]
    bpy.context.window.scene = scene
    # Export only the original main scene's mesh objects and their existing rigs.
    # Exclude lights/cameras/custom bone display helpers. No source data is saved.
    for obj in scene.objects:
        obj.select_set(False)
    meshes = [obj for obj in scene.objects if obj.type == "MESH" and not obj.name.startswith("Icosphere")]
    if not meshes:
        raise RuntimeError(f"No character mesh in main scene: {source}")
    selected = set(meshes)
    for mesh in meshes:
        for modifier in mesh.modifiers:
            if modifier.type == "ARMATURE" and modifier.object is not None:
                selected.add(modifier.object)
        parent = mesh.parent
        while parent is not None:
            selected.add(parent)
            parent = parent.parent
    for obj in selected:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    output = ROOT / entry["Runtime"]
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB",
                              use_selection=True, use_active_scene=True,
                              export_animations=False, export_cameras=False,
                              export_lights=False)
    raw = output.read_bytes()
    content = json.loads(raw[20:20 + struct.unpack_from("<I", raw, 12)[0]])
    if len(content.get("scenes", [])) != 1 or len(content.get("meshes", [])) != len(meshes):
        raise RuntimeError(f"Unexpected extra scene/mesh in export: {output}")
    if content.get("animations"):
        raise RuntimeError(f"Unexpected animation clips in static export: {output}")
    entry["Source"] = entry["OriginalBlenderSource"]
    entry["SHA256"] = hashlib.sha256(output.read_bytes()).hexdigest().upper()
    entry["ExportedMeshCount"] = len(meshes)
    entry["ExportedVertexCount"] = sum(len(obj.data.vertices) for obj in meshes)
    if hashlib.sha256(source.read_bytes()).hexdigest().upper() != digest:
        raise RuntimeError(f"Original source unexpectedly changed: {source}")
    print(f"BETA EXPORTED {entry['Model']}: {len(meshes)} meshes -> {output}", flush=True)

if not inspect_only:
    MANIFEST.write_text(json.dumps(entries, indent=2) + "\n", encoding="utf-8")
    print("BETA ORIGINAL EXPORTS COMPLETE; all original Blender checksums unchanged.", flush=True)
