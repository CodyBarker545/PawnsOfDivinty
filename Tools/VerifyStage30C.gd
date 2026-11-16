extends SceneTree

var mode="after"
var arena:Node3D
var camera:Camera3D
var tag:String

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--cliff-mode="):mode=arg.split("=")[1]
    tag="stage30c_"+mode
    call_deferred("review")

func shot(suffix:String,viewport:Viewport):
    for i in 4:await process_frame
    await RenderingServer.frame_post_draw
    viewport.get_texture().get_image().save_png("res://artifacts/"+tag+"_"+suffix+".png")

func median(values:Array):
    values.sort()
    return values[values.size()/2]

func profile_view(label:String):
    # Allow lazy rendering allocations and the once-per-second Performance monitors to settle.
    var warm_until=Time.get_ticks_msec()+2200
    while Time.get_ticks_msec()<warm_until:await process_frame
    var draws=[];var primitives=[];var objects=[];var frame_ms=[];var process_ms=[];var video=[]
    var last=Time.get_ticks_usec()
    for i in 120:
        await process_frame
        var now=Time.get_ticks_usec();frame_ms.append((now-last)/1000.0);last=now
        draws.append(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME))
        primitives.append(Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME))
        objects.append(Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME))
        process_ms.append(Performance.get_monitor(Performance.TIME_PROCESS)*1000)
        video.append(Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED))
    return {"view":label,"samples":120,"draw_calls_median":median(draws),"rendered_primitives_median":median(primitives),"rendered_objects_median":median(objects),"frame_interval_ms_median":median(frame_ms),"process_ms_median":median(process_ms),"video_memory_bytes_median":median(video)}

func frame_at(focal:Vector3,size:float,offset:Vector3):
    camera.size=size;camera.global_position=focal+offset;camera.look_at(focal,Vector3.UP)

func inspect_geometry():
    var art=arena.get_node("ConnectedCliffEnvironment")
    assert(art.find_children("*","CollisionObject3D",true,false).is_empty())
    assert(art.find_children("*","CollisionShape3D",true,false).is_empty())
    assert(art.find_children("*","NavigationRegion3D",true,false).is_empty())
    var meshes={};var total=0;var surfaces=0;var instances=0;var mat_ids={};var triangles=[]
    for part in art.find_children("*","MeshInstance3D",true,false):
        instances+=1;var id=part.mesh.get_instance_id();var tris=0
        for s in part.mesh.get_surface_count():
            var a=part.mesh.surface_get_arrays(s);var idx=a[Mesh.ARRAY_INDEX];var v=a[Mesh.ARRAY_VERTEX]
            tris+=idx.size()/3;surfaces+=1
            var mat=part.get_active_material(s) as StandardMaterial3D
            assert(mat and mat.vertex_color_use_as_albedo and mat.albedo_texture and mat.normal_texture)
            mat_ids[mat.get_instance_id()]=mat.resource_path
            for k in range(0,idx.size(),3):triangles.append([part.global_transform*v[idx[k]],part.global_transform*v[idx[k+1]],part.global_transform*v[idx[k+2]]])
        total+=tris
        if not meshes.has(id):meshes[id]={"path":part.mesh.resource_path,"triangles":tris,"uses":0}
        meshes[id].uses+=1
    var samples=[]
    # Walkable terrain next to the expanded cliff and both stair corridors.
    var all_triangles=triangles.duplicate()
    var western=arena.get_node("WesternStairEnvironment")
    for part in western.find_children("*","MeshInstance3D",true,false):
        for s in part.mesh.get_surface_count():
            var a=part.mesh.surface_get_arrays(s);var v=a[Mesh.ARRAY_VERTEX];var idx=a[Mesh.ARRAY_INDEX]
            for k in range(0,idx.size(),3):all_triangles.append([part.global_transform*v[idx[k]],part.global_transform*v[idx[k+1]],part.global_transform*v[idx[k+2]]])
    for y in [14,15,16]:
        for x in range(30):
            var n=arena.get_node("Terrain/tile_%d_%d"%[x,y])
            if n.BlocksMovement:continue
            # Eastern authored stairs are intentionally retained and validated by native stair checks.
            if x in [24,25] and y in [15,16]:continue
            var px=-2.125+x*1.25;var pz=4.125+y*1.25;var highest=-100.0
            for t in all_triangles:
                if px<min(t[0].x,min(t[1].x,t[2].x)) or px>max(t[0].x,max(t[1].x,t[2].x)):continue
                if pz<min(t[0].z,min(t[1].z,t[2].z)) or pz>max(t[0].z,max(t[1].z,t[2].z)):continue
                var hit=Geometry3D.segment_intersects_triangle(Vector3(px,3,pz),Vector3(px,-4,pz),t[0],t[1],t[2])
                if hit!=null:highest=max(highest,hit.y)
            assert(abs(highest-n.Elevation*0.6)<0.026,"Standing mismatch %d,%d: %f"%[x,y,highest])
            samples.append({"x":x,"y":y,"visual_y":highest,"logical_y":n.Elevation*0.6})
    # Above-foot rocks must remain within authored cliff blocker bands, never in a stair opening.
    for t in triangles:
        for p in t:
            if p.y>-.3 and p.z>22.25 and p.z<23.5:
                assert(not(p.x>2.251 and p.x<4.749) and not(p.x>27.251 and p.x<29.749),"Rock entered stair clearance")
    var unique=0
    for m in meshes.values():unique+=m.triangles
    var eastern_tread_count=0
    for x in [24,25]:
        for y in [15,16]:
            for tread in arena.get_node("Terrain/tile_%d_%d"%[x,y]).get_children():
                if not tread is MeshInstance3D:continue
                assert(tread.visible and tread.material_override.resource_path.ends_with("EasternStepSlate.tres"))
                eastern_tread_count+=1
    assert(eastern_tread_count==20)
    var result={"mesh_instance_count":instances,"unique_mesh_count":meshes.size(),"unique_triangles":unique,"instance_expanded_triangles":total,"surfaces":surfaces,"distinct_materials":mat_ids.size(),"meshes":meshes.values(),"standing_samples":samples,"collision_nodes":0,"eastern_original_treads_restyled":eastern_tread_count,"eastern_added_geometry":0}
    var f=FileAccess.open("res://artifacts/stage30c_geometry.json",FileAccess.WRITE);f.store_string(JSON.stringify(result,"  "));f.close()
    print("CONNECTED CLIFF GEOMETRY PASSED: ",meshes.size()," unique meshes, ",instances," instances, ",total," rendered triangles before culling; ",samples.size()," standing centers")
    return true

func review():
    DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
    Engine.max_fps=0
    var path="res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn"
    if mode=="stage30b":path="res://SourceAssets/Environment/ConnectedCliff/Baseline/CliffsideArena.tscn"
    if mode=="greybox":path="res://SourceAssets/Environment/WesternStairSlice/Baseline/CliffsideArena.tscn"
    arena=load(path).instantiate();root.add_child(arena)
    for i in 12:await process_frame
    arena.process_mode=Node.PROCESS_MODE_DISABLED;arena.find_child("RuntimeHover",true,false).hide()
    camera=arena.find_child("Camera3D",true,false)
    var normal_transform=camera.global_transform;var normal_size=camera.size
    if mode=="after":
        var valid=inspect_geometry()
        if not valid:
            quit(1)
            return
    await shot("tactical",root)
    var profiles=[];profiles.append(await profile_view("normal tactical 1280x720"))
    frame_at(Vector3(16,-.7,23.4),43,Vector3(9,13,23))
    await shot("full_cliff",camera.get_viewport());profiles.append(await profile_view("angled full cliff"))
    frame_at(Vector3(3.5,-.6,23.35),12,Vector3(7,8,11));await shot("west",camera.get_viewport())
    frame_at(Vector3(16,-.65,23.5),11,Vector3(4,5,9));await shot("central",camera.get_viewport())
    frame_at(Vector3(28.5,-.6,23.35),12,Vector3(-7,8,11));await shot("east",camera.get_viewport())
    var f=FileAccess.open("res://artifacts/"+tag+"_profile.json",FileAccess.WRITE);f.store_string(JSON.stringify({"mode":mode,"vsync":"disabled","warmup_ms_per_view":2200,"profiles":profiles},"  "));f.close()
    print(tag.to_upper()," CAPTURE AND PROFILE PASSED")
    if "--stay" in OS.get_cmdline_user_args():
        DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED)
        Engine.max_fps=60
        frame_at(Vector3(16,-.7,23.4),43,Vector3(9,13,23));return
    arena.queue_free();await process_frame;quit(0)
