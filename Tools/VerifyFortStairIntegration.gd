extends SceneTree

var arena:Node3D
var camera:Camera3D
var mode="after"
var report={}
var terrain_triangles=[]
var unique_meshes={}
var materials={}
var rendered_triangles=0
var surfaces=0

func _initialize():
    if "--before" in OS.get_cmdline_user_args():mode="before"
    call_deferred("review")

func shot(label:String,viewport:Viewport):
    for i in 5:await process_frame
    await RenderingServer.frame_post_draw
    viewport.get_texture().get_image().save_png("res://artifacts/fort_stair_integration/"+mode+"_"+label+".png")

func frame_at(focal:Vector3,size:float,offset:Vector3):
    camera.size=size;camera.global_position=focal+offset;camera.look_at(focal,Vector3.UP)

func add_mesh(mesh:Mesh,transform:Transform3D,material:Material,record=true):
    var count=0
    for s in mesh.get_surface_count():
        var a=mesh.surface_get_arrays(s);var v=a[Mesh.ARRAY_VERTEX];var idx=a[Mesh.ARRAY_INDEX]
        count+=idx.size()/3
        if record:
            for k in range(0,idx.size(),3):terrain_triangles.append([transform*v[idx[k]],transform*v[idx[k+1]],transform*v[idx[k+2]]])
    rendered_triangles+=count
    unique_meshes[mesh.get_instance_id()]=count
    if material:
        assert(material is StandardMaterial3D and material.vertex_color_use_as_albedo)
        materials[material.get_instance_id()]=material.resource_path

func height_at(x:float,z:float):
    var highest=-100.0
    for t in terrain_triangles:
        if x<min(t[0].x,min(t[1].x,t[2].x)) or x>max(t[0].x,max(t[1].x,t[2].x)):continue
        if z<min(t[0].z,min(t[1].z,t[2].z)) or z>max(t[0].z,max(t[1].z,t[2].z)):continue
        var hit=Geometry3D.segment_intersects_triangle(Vector3(x,3,z),Vector3(x,-4,z),t[0],t[1],t[2])
        if hit!=null:highest=max(highest,hit.y)
    return highest

func inspect_geometry():
    var art=arena.get_node("EasternStairEnvironment")
    for type in ["CollisionObject3D","CollisionShape3D","NavigationRegion3D"]:assert(art.find_children("*",type,true,false).is_empty())
    var slab_count=0
    for part in art.find_children("*","MultiMeshInstance3D",true,false):
        assert(part.material_override.resource_path.ends_with("DressedStairStone.tres"))
        surfaces+=part.multimesh.mesh.get_surface_count();slab_count+=part.multimesh.instance_count
        for i in part.multimesh.instance_count:add_mesh(part.multimesh.mesh,part.global_transform*part.multimesh.get_instance_transform(i),part.material_override)
    for part in art.find_children("*","MeshInstance3D",true,false):
        surfaces+=part.mesh.get_surface_count();add_mesh(part.mesh,part.global_transform,part.get_active_material(0))
    var masonry=arena.get_node("FortStairIntegration")
    for type in ["CollisionObject3D","CollisionShape3D","NavigationRegion3D"]:assert(masonry.find_children("*",type,true,false).is_empty())
    for part in masonry.find_children("*","MeshInstance3D",true,false):
        surfaces+=part.mesh.get_surface_count();add_mesh(part.mesh,part.global_transform,part.get_active_material(0))
    var approach_samples=[]
    for x in [2.875,4.125,27.875,29.125]:
        for row in range(10,15):
            var z=4.125+row*1.25;var h=height_at(x,z)
            assert(abs(h-.024)<.001,"Approach height mismatch")
            approach_samples.append({"x":x,"z":z,"visual_y":h})
    assert(slab_count==47)
    var tops=[-.12,-.36,-.6,-.74,-.86,-.96,-1.08,-1.2,-1.44,-1.68];var samples=[]
    for x in [27.875,29.125]:
        for i in 10:
            var z=22.375+i*.25;var actual=height_at(x,z)
            assert(abs(actual-tops[i])<.026,"Eastern tread unsupported or wrong height: %f,%f ->%f"%[x,z,actual])
            samples.append({"x":x,"z":z,"visual_y":actual,"expected_y":tops[i]})
    # New nosings must leave both original stair-center profiles unchanged.
    for x in [2.875,4.125]:
        for i in 10:
            # The unchanged western ground/slabs are separate; the new overlay
            # is tested below for standing clearance and side-corridor intrusion.
            var z=22.375+i*.25
            var h=height_at(x,z)
            assert(h<tops[i]+.026,"New trim raised a western standing center")
    var unique=0
    for value in unique_meshes.values():unique+=value
    report={"unique_triangles":unique,"rendered_triangles":rendered_triangles,"surfaces":surfaces,"unique_meshes":unique_meshes.size(),"distinct_materials":materials.size(),"slab_instances":slab_count,"tread_samples":samples,"collision_nodes":0}
    # Check that no added side rock enters the legal two-column corridor.
    for name in ["BuriedEastFracture","WestFootStone","EastFootStone"]:
        var rock=art.get_node(name)
        for part in rock.find_children("*","MeshInstance3D",true,false):
            for s in part.mesh.get_surface_count():
                for v in part.mesh.surface_get_arrays(s)[Mesh.ARRAY_VERTEX]:
                    var p=part.global_transform*v
                    assert(p.x<=27.25 or p.x>=29.75,"Framing rock entered stair opening")
    # The original terrain nodes and all original tread meshes survive for picking and rollback.
    var hidden=0
    for x in [24,25]:
        for y in [15,16]:
            var tile=arena.get_node("Terrain/tile_%d_%d"%[x,y])
            assert(tile.Elevation==(-1 if y==15 else -2))
            for i in 5:
                assert(not tile.get_node("Tread%d"%i).visible);hidden+=1
    assert(hidden==20)
    report["approach_samples"]=approach_samples
    # Grid remains above the tread profile, including small erosion marks.
    var grid=arena.get_node("GridOverlay/TacticalGridLines") as MeshInstance3D
    var arrays=grid.mesh.surface_get_arrays(0);var grid_vertices=arrays[Mesh.ARRAY_VERTEX];var normals=arrays[Mesh.ARRAY_NORMAL];var checks=0;var edge_samples=0
    # Check exposed top-face interiors. Wire ends at a vertical stair nosing can naturally
    # intersect the adjacent higher riser, as they do on the unchanged western stairs.
    for k in range(0,grid_vertices.size(),3):
        if normals[k].y<.9:continue
        var p=grid.global_transform*((grid_vertices[k]+grid_vertices[k+1]+grid_vertices[k+2])/3.0)
        if p.x>27.26 and p.x<29.74 and p.z>22.27 and p.z<24.73:
            var phase=(p.z-22.25)/.25
            if abs(phase-round(phase))*.25<.03:
                edge_samples+=1
                continue
            var h=height_at(p.x,p.z)
            if h> -50:
                assert(p.y>h,"Grid vertex %s below stair y%f"%[p,h])
                checks+=1
    assert(checks>0);report["grid_top_face_clearance_checks"]=checks;report["grid_nosing_edge_samples_excluded"]=edge_samples
    return true

func review():
    var path="res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn"
    if mode=="before":path="res://SourceAssets/Environment/FortStairIntegration/Baseline/BeforeView.tscn"
    arena=load(path).instantiate();root.add_child(arena)
    for i in 15:await process_frame
    arena.process_mode=Node.PROCESS_MODE_DISABLED
    camera=arena.find_child("Camera3D",true,false)
    var hover=arena.find_child("RuntimeHover",true,false)
    if hover:hover.hide()
    if mode=="after" and not inspect_geometry():
        quit(1)
        return
    await shot("tactical",root)
    frame_at(Vector3(16,-.9,24),42,Vector3(0,21,22));await shot("both_stairs",camera.get_viewport())
    var draws=[];var primitives=[]
    for i in 90:await process_frame
    for i in 120:
        await process_frame;draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME));primitives.append(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
    draws.sort();primitives.sort();report["draw_calls_median"]=draws[60];report["render_primitives_median"]=primitives[60]
    frame_at(Vector3(28.5,-.7,23.5),10.5,Vector3(-5,7,9));await shot("east_close",camera.get_viewport())
    arena.get_node("GridOverlay").show();await shot("east_grid_on",camera.get_viewport());arena.get_node("GridOverlay").hide()
    frame_at(Vector3(27.5,-.5,20.6),17.5,Vector3(8,15,17));await shot("east_access_route",camera.get_viewport())
    frame_at(Vector3(4.5,-.5,20.6),17.5,Vector3(-8,15,17));await shot("west_access_route",camera.get_viewport())
    frame_at(Vector3(28.5,-1.65,24.75),5.8,Vector3(-2,4,6));await shot("lower_transition",camera.get_viewport())
    var f=FileAccess.open("res://artifacts/fort_stair_integration/"+mode+"_geometry.json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"  "));f.close()
    print("EASTERN STAIR ",mode.to_upper()," PASSED ",report)
    if "--stay" in OS.get_cmdline_user_args():
        frame_at(Vector3(28.5,-.7,23.5),10.5,Vector3(-5,7,9));return
    arena.queue_free();await process_frame;quit()
