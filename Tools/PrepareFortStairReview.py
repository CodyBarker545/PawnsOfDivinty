"""Derive a focused review from the existing stair geometry verifier."""
from pathlib import Path
R=Path(__file__).resolve().parents[1]
t=(R/'Tools/VerifyStage30D2B.gd').read_text().replace('artifacts/stage30d2b/','artifacts/fort_stair_integration/').replace('SourceAssets/Environment/EasternStair/Baseline/CliffsideArena.tscn','SourceAssets/Environment/FortStairIntegration/Baseline/BeforeView.tscn')
baseline=R/'SourceAssets/Environment/FortStairIntegration/Baseline'
(baseline/'BeforeView.tscn').write_text((baseline/'CliffsideArena.tscn').read_text().replace('res://Assets/Levels/Environment/EasternStair/EasternStair.tscn','res://SourceAssets/Environment/FortStairIntegration/Baseline/EasternStair.tscn'))
t=t.replace('part.material_override.resource_path.ends_with("SliceMaterial3.tres")','part.material_override.resource_path.ends_with("DressedStairStone.tres")')
t=t.replace('    assert(slab_count==47)','''    var masonry=arena.get_node("FortStairIntegration")
    for type in ["CollisionObject3D","CollisionShape3D","NavigationRegion3D"]:assert(masonry.find_children("*",type,true,false).is_empty())
    for part in masonry.find_children("*","MeshInstance3D",true,false):
        surfaces+=part.mesh.get_surface_count();add_mesh(part.mesh,part.global_transform,part.get_active_material(0))
    var approach_samples=[]
    for x in [2.875,4.125,27.875,29.125]:
        for row in range(10,15):
            var z=4.125+row*1.25;var h=height_at(x,z)
            assert(abs(h-.024)<.001,"Approach height mismatch")
            approach_samples.append({"x":x,"z":z,"visual_y":h})
    assert(slab_count==47)''')
t=t.replace('    var unique=0','''    # New nosings must leave both original stair-center profiles unchanged.
    for x in [2.875,4.125]:
        for i in 10:
            # The unchanged western ground/slabs are separate; the new overlay
            # is tested below for standing clearance and side-corridor intrusion.
            var z=22.375+i*.25
            var h=height_at(x,z)
            assert(h<tops[i]+.026,"New trim raised a western standing center")
    var unique=0''')
t=t.replace('    # Grid remains above','    report["approach_samples"]=approach_samples\n    # Grid remains above')
t=t.replace('    frame_at(Vector3(28.5,-1.65,24.75)', '''    frame_at(Vector3(27.5,-.5,20.6),17.5,Vector3(8,15,17));await shot("east_access_route",camera.get_viewport())
    frame_at(Vector3(4.5,-.5,20.6),17.5,Vector3(-8,15,17));await shot("west_access_route",camera.get_viewport())
    frame_at(Vector3(28.5,-1.65,24.75)''')
(R/'Tools/VerifyFortStairIntegration.gd').write_text(t)
print('Prepared same-camera before/after fort route and stair review.')
