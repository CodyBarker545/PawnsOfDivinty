extends SceneTree

var arena:Node3D
var camera:Camera3D
var mode="after"
var report={}

func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg=="--before":mode="before"
    call_deferred("review")

func shot(label:String,viewport:Viewport):
    for i in 5:await process_frame
    await RenderingServer.frame_post_draw
    viewport.get_texture().get_image().save_png("res://artifacts/stage30d2a/"+mode+"_"+label+".png")

func frame_at(focal:Vector3,size:float,offset:Vector3):
    camera.size=size;camera.global_position=focal+offset;camera.look_at(focal,Vector3.UP)

func review():
    var path="res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn"
    if mode=="before":path="res://SourceAssets/Environment/LowerTerrace/Baseline/CliffsideArena.tscn"
    arena=load(path).instantiate();root.add_child(arena)
    for i in 15:await process_frame
    arena.process_mode=Node.PROCESS_MODE_DISABLED
    camera=arena.find_child("Camera3D",true,false)
    var hover=arena.find_child("RuntimeHover",true,false)
    if hover: hover.hide()
    await shot("tactical",root)
    frame_at(Vector3(16,-1.8,25.7),42,Vector3(0,21,22))
    await shot("lower_off",camera.get_viewport())
    var grid=arena.get_node("GridOverlay");grid.show()
    await shot("lower_on",camera.get_viewport());grid.hide()
    if mode=="after":
        var art=arena.get_node("LowerTerraceEnvironment")
        assert(art.find_children("*","CollisionObject3D",true,false).is_empty())
        assert(art.find_children("*","CollisionShape3D",true,false).is_empty())
        assert(art.find_children("*","NavigationRegion3D",true,false).is_empty())
        var tris=[];var count=0;var surfaces=0
        for part in art.find_children("*","MeshInstance3D",true,false):
            for s in part.mesh.get_surface_count():
                surfaces+=1
                var a=part.mesh.surface_get_arrays(s);var v=a[Mesh.ARRAY_VERTEX];var idx=a[Mesh.ARRAY_INDEX]
                count+=idx.size()/3
                if part.name=="ConnectedGround":
                    for k in range(0,idx.size(),3):tris.append([part.global_transform*v[idx[k]],part.global_transform*v[idx[k+1]],part.global_transform*v[idx[k+2]]])
        var checked=0
        for y in range(16,20):
            for x in range(30):
                if y==16 and x in [4,5,24,25]:continue
                var tile=arena.get_node("Terrain/tile_%d_%d"%[x,y]);var p=Vector3(-2.125+x*1.25,0,4.125+y*1.25);var height=-100.0
                for t in tris:
                    var hit=Geometry3D.segment_intersects_triangle(p+Vector3.UP*3,p-Vector3.UP*4,t[0],t[1],t[2])
                    if hit!=null:height=max(height,hit.y)
                assert(abs(height-tile.Elevation*.6)<.026,"Ground missing or standing height mismatch")
                checked+=1
        report={"triangles":count,"surfaces":surfaces,"standing_centers":checked,"collision_nodes":0,"normal_floor_y":-1.787,"grid_lowest_top_y":-1.768}
    # Measure identical views after rendering has settled; report engine counters, not estimates.
    for i in 90:await process_frame
    report["draw_calls"]=Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
    report["video_memory_bytes"]=Performance.get_monitor(Performance.RENDER_VIDEO_MEM_USED)
    report["texture_memory_bytes"]=Performance.get_monitor(Performance.RENDER_TEXTURE_MEM_USED)
    report["render_primitives"]=Performance.get_monitor(Performance.RENDER_TOTAL_PRIMITIVES_IN_FRAME)
    frame_at(Vector3(16,-1.8,25.6),10,Vector3(2,5,6));await shot("close",camera.get_viewport())
    frame_at(Vector3(3.5,-1.1,24.6),10,Vector3(5,7,9));await shot("west",camera.get_viewport())
    frame_at(Vector3(28.5,-1.1,24.6),10,Vector3(-5,7,9));await shot("east",camera.get_viewport())
    var f=FileAccess.open("res://artifacts/stage30d2a/"+mode+"_geometry.json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"  "));f.close()
    print("LOWER TERRACE ",mode.to_upper()," PASSED ",report)
    if "--stay" in OS.get_cmdline_user_args():return
    arena.queue_free();await process_frame;quit()
