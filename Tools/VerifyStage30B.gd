extends SceneTree

var before = "--before" in OS.get_cmdline_user_args()
var tag = "stage30b_before" if before else "stage30b_after"

func _initialize():
    call_deferred("review")

func save_image(viewport: Viewport, suffix: String):
    for i in 3: await process_frame
    await RenderingServer.frame_post_draw
    viewport.get_texture().get_image().save_png("res://artifacts/"+tag+"_"+suffix+".png")

func review():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    root.add_child(arena)
    for i in 12: await process_frame
    arena.process_mode = Node.PROCESS_MODE_DISABLED
    arena.find_child("RuntimeHover",true,false).hide()
    var camera = arena.find_child("Camera3D",true,false) as Camera3D
    var records=[]
    for y in range(13,19):
        for x in range(2,8):
            var n=arena.get_node("Terrain/tile_%d_%d" % [x,y])
            records.append({"x":x,"y":y,"position":str(n.global_position),"elevation":n.Elevation,"movement_blocked":n.BlocksMovement,"sight_blocked":n.BlocksSight})
    var f=FileAccess.open("res://artifacts/"+tag+"_measurements.json",FileAccess.WRITE)
    f.store_string(JSON.stringify({"cells":records,"camera_size":camera.size,"camera_transform":str(camera.global_transform)},"  ")); f.close()
    if not before:
        var art=arena.get_node("WesternStairEnvironment")
        assert(art.find_children("*","CollisionObject3D",true,false).is_empty())
        assert(art.find_children("*","CollisionShape3D",true,false).is_empty())
        assert(art.find_children("*","NavigationRegion3D",true,false).is_empty())
        assert(art.position==Vector3.ZERO)
        var triangles=[]
        var runtime_metrics=[]
        for part in art.find_children("*","MeshInstance3D",true,false):
            var count=0
            for surface in part.mesh.get_surface_count():
                var mat=part.get_active_material(surface) as StandardMaterial3D
                assert(mat and mat.vertex_color_use_as_albedo and mat.albedo_texture and mat.normal_texture)
                assert(mat.metallic==0 and abs(mat.roughness-0.9)<0.001)
                var arrays=part.mesh.surface_get_arrays(surface)
                assert(arrays[Mesh.ARRAY_COLOR].size()>0)
                var v=arrays[Mesh.ARRAY_VERTEX]
                var idx=arrays[Mesh.ARRAY_INDEX]
                count+=idx.size()/3
                for k in range(0,idx.size(),3):
                    triangles.append([part.global_transform*v[idx[k]],part.global_transform*v[idx[k+1]],part.global_transform*v[idx[k+2]]])
            runtime_metrics.append({"mesh":str(part.name),"triangles":count,"surfaces":part.mesh.get_surface_count()})
        var samples=[]
        for record in records:
            if record.movement_blocked:continue
            var px=-2.125+record.x*1.25
            var pz=4.125+record.y*1.25
            var highest=-100.0
            for t in triangles:
                if px<min(t[0].x,min(t[1].x,t[2].x)) or px>max(t[0].x,max(t[1].x,t[2].x)):continue
                if pz<min(t[0].z,min(t[1].z,t[2].z)) or pz>max(t[0].z,max(t[1].z,t[2].z)):continue
                var hit=Geometry3D.segment_intersects_triangle(Vector3(px,3,pz),Vector3(px,-4,pz),t[0],t[1],t[2])
                if hit!=null:highest=max(highest,hit.y)
            var expected=record.elevation*0.6
            assert(abs(highest-expected)<0.026,"Visual standing height mismatch at %d,%d: %f expected %f" % [record.x,record.y,highest,expected])
            samples.append({"x":record.x,"y":record.y,"visual_height":highest,"gameplay_height":expected})
        var metrics_file=FileAccess.open("res://artifacts/stage30b_runtime_geometry.json",FileAccess.WRITE)
        metrics_file.store_string(JSON.stringify({"meshes":runtime_metrics,"standing_samples":samples,"collision_objects":0},"  "))
        metrics_file.close()
        print("STANDING HEIGHTS PASSED: ",samples.size()," walkable centers; max tolerance 0.026; four vertex-color materials, no collision/navigation.")
    await save_image(root,"tactical")
    camera.size=12.0
    var focal=Vector3(3.5,-0.6,23.35)
    camera.global_position=focal+Vector3(7,8,11)
    camera.look_at(focal,Vector3.UP)
    await save_image(camera.get_viewport(),"close")
    print(tag.to_upper()+" VISUAL CAPTURE AND COLLISION CHECK PASSED")
    if "--stay" in OS.get_cmdline_user_args(): return
    quit(0)
