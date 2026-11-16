extends SceneTree

var arena:Node3D
var camera:Camera3D
var report={}
var triangles=[]
var cliff_triangles=[]
var unique={}
var rendered=0
var surfaces=0

func _initialize():call_deferred("review")

func add_mesh(mesh:Mesh,t:Transform3D,is_cliff=false):
    var count=0
    for s in mesh.get_surface_count():
        var a=mesh.surface_get_arrays(s);var v=a[Mesh.ARRAY_VERTEX];var idx=a[Mesh.ARRAY_INDEX];count+=idx.size()/3
        for k in range(0,idx.size(),3):
            var tri=[t*v[idx[k]],t*v[idx[k+1]],t*v[idx[k+2]]];triangles.append(tri)
            if is_cliff:cliff_triangles.append(tri)
    rendered+=count;unique[mesh.get_instance_id()]=count

func height(x:float,z:float):
    var top=-100.0
    for t in triangles:
        if x<min(t[0].x,min(t[1].x,t[2].x)) or x>max(t[0].x,max(t[1].x,t[2].x)):continue
        if z<min(t[0].z,min(t[1].z,t[2].z)) or z>max(t[0].z,max(t[1].z,t[2].z)):continue
        var hit=Geometry3D.segment_intersects_triangle(Vector3(x,3,z),Vector3(x,-4,z),t[0],t[1],t[2])
        if hit!=null:top=max(top,hit.y)
    return top

func inspect():
    var art=arena.get_node("ForwardCastleStairs")
    for kind in ["CollisionObject3D","CollisionShape3D","NavigationRegion3D"]:assert(art.find_children("*",kind,true,false).is_empty())
    for p in art.find_children("*","MeshInstance3D",true,false):
        if not p.is_visible_in_tree():continue
        surfaces+=p.mesh.get_surface_count();add_mesh(p.mesh,p.global_transform,p.name=="CliffRock")
    for p in art.find_children("*","MultiMeshInstance3D",true,false):
        surfaces+=p.multimesh.mesh.get_surface_count()
        for i in p.multimesh.instance_count:add_mesh(p.multimesh.mesh,p.global_transform*p.multimesh.get_instance_transform(i))
    var standing=[]
    for y in range(14,20):
        for x in range(30):
            var tile=arena.get_node("Terrain/tile_%d_%d"%[x,y])
            if tile.BlocksMovement:continue
            var actual=height(-2.125+x*1.25,4.125+y*1.25);var expected=tile.Elevation*.6
            assert(abs(actual-expected)<.026,"Standing support mismatch %d,%d actual%f expected%f"%[x,y,actual,expected])
            standing.append({"x":x,"y":y,"visual_y":actual,"expected_y":expected})
    var closed=[]
    for x in [4,5,24,25]:
        var px=-2.125+x*1.25;var hits=0
        for t in cliff_triangles:
            if Geometry3D.segment_intersects_triangle(Vector3(px,-.6,22.1),Vector3(px,-.6,23.6),t[0],t[1],t[2])!=null:hits+=1
        assert(hits>0,"Old stair opening still has a visual cliff gap");closed.append({"x":x,"rock_face_hits":hits})
    # The derived geological mesh has actual empty stair apertures, not a shader disguise.
    for t in cliff_triangles:
        var p=(t[0]+t[1]+t[2])/3
        if p.z>22.251 and p.z<24.749 and p.y> -1.78:
            assert(not(p.x>7.251 and p.x<9.749) and not(p.x>22.251 and p.x<24.749),"Cliff blocks new castle stair opening")
    for n in ["WesternStairEnvironment","ConnectedCliffEnvironment","LowerTerraceEnvironment","EasternStairEnvironment","FortStairIntegration"]:assert(not arena.get_node(n).visible)
    var total_unique=0
    for count in unique.values():total_unique+=count
    report={"standing_samples":standing,"old_openings_closed":closed,"collision_nodes":0,"surfaces":surfaces,"unique_meshes":unique.size(),"unique_triangles":total_unique,"instance_expanded_triangles":rendered}
    # Check exposed grid top-face interiors at each tread, excluding wire/riser intersections.
    var grid=arena.get_node("GridOverlay/TacticalGridLines");var a=grid.mesh.surface_get_arrays(0);var v=a[Mesh.ARRAY_VERTEX];var normals=a[Mesh.ARRAY_NORMAL];var checks=0
    for i in range(0,v.size(),3):
        if normals[i].y<.9:continue
        var p=grid.global_transform*((v[i]+v[i+1]+v[i+2])/3)
        if ((p.x>7.26 and p.x<9.74) or (p.x>22.26 and p.x<24.74)) and p.z>22.27 and p.z<24.73:
            var phase=(p.z-22.25)/.25
            if abs(phase-round(phase))*.25<.03:continue
            assert(p.y>height(p.x,p.z),"Grid buried on new stairs");checks+=1
    assert(checks>0);report["stair_grid_clearance_samples"]=checks
    return true

func shot(label:String,viewport:Viewport):
    for i in 5:await process_frame
    await RenderingServer.frame_post_draw
    viewport.get_texture().get_image().save_png("res://artifacts/stage30d2c/"+label+".png")

func frame_at(focal:Vector3,size:float,offset:Vector3):
    camera.size=size;camera.global_position=focal+offset;camera.look_at(focal,Vector3.UP)

func review():
    arena=load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate();root.add_child(arena)
    for i in 15:await process_frame
    arena.process_mode=Node.PROCESS_MODE_DISABLED;camera=arena.find_child("Camera3D",true,false)
    if not inspect():quit(1);return
    var original=camera.global_transform;var original_size=camera.size
    await shot("tactical_off",root);arena.get_node("GridOverlay").show();await shot("tactical_on",root);arena.get_node("GridOverlay").hide()
    frame_at(Vector3(16,-.5,20.5),42,Vector3(0,32,.01));await shot("overhead",camera.get_viewport())
    frame_at(Vector3(8.5,-.5,22.8),13,Vector3(-6,10,13));await shot("west_castle_stairs",camera.get_viewport())
    frame_at(Vector3(23.5,-.5,22.8),13,Vector3(6,10,13));await shot("east_castle_stairs",camera.get_viewport())
    arena.get_node("GridOverlay").show();await shot("east_grid_on",camera.get_viewport());arena.get_node("GridOverlay").hide()
    frame_at(Vector3(3.5,-.65,23.3),11,Vector3(3,8,11));await shot("old_west_repaired",camera.get_viewport())
    frame_at(Vector3(28.5,-.65,23.3),11,Vector3(-3,8,11));await shot("old_east_repaired",camera.get_viewport())
    frame_at(Vector3(16,-.7,23.4),43,Vector3(0,21,24));await shot("full_cliff",camera.get_viewport())
    for i in 90:await process_frame
    report["draw_calls"]=Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)
    var f=FileAccess.open("res://artifacts/stage30d2c/geometry.json",FileAccess.WRITE);f.store_string(JSON.stringify(report,"  "));f.close()
    print("FORWARD CASTLE STAIR GEOMETRY PASSED: ",report.standing_samples.size()," standing centers; old holes closed; new apertures clear; ",rendered," instance-expanded triangles")
    if "--stay" in OS.get_cmdline_user_args():
        frame_at(Vector3(16,-.5,22.8),43,Vector3(0,23,25));return
    arena.queue_free();await process_frame;quit()
