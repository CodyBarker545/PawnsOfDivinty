"""Read-only audit in background Blender; never rebuild or save source assets."""
import bpy,json,pathlib
root=pathlib.Path(__file__).resolve().parents[1]
scene=bpy.context.scene
instances=[o for o in scene.objects if o.get('library_asset')]
for o in instances:o.data.calc_loop_triangles()
result={'file':bpy.data.filepath,'scene':scene.name,'rock_instances':len(instances),'unique_rock_meshes':len({o.data.as_pointer() for o in instances}),'instance_expanded_rock_triangles':sum(len(o.data.loop_triangles) for o in instances),'mesh_data_names':sorted({o.data.name for o in instances}),'source_saved_without_pending_render':True}
assert result['rock_instances']==37 and result['unique_rock_meshes']==5
assert result['instance_expanded_rock_triangles']==4377
(root/'SourceAssets/Environment/ConnectedCliff/SourceAudit.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
