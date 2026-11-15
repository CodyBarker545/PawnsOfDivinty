extends SceneTree

func _initialize():
    call_deferred("capture")

func capture():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    root.add_child(arena)
    for i in 8:
        await process_frame
    arena.process_mode = Node.PROCESS_MODE_DISABLED
    await RenderingServer.frame_post_draw
    var stage = "stage13a" if "--stage13a" in OS.get_cmdline_user_args() else "stage11"
    if "--stage14a" in OS.get_cmdline_user_args():
        stage = "stage14a"
    elif "--stage15a" in OS.get_cmdline_user_args():
        stage = "stage15a"
    var angled = root.get_texture().get_image().save_png("res://artifacts/%s_gameplay.png" % stage)
    var overhead_camera = Camera3D.new()
    overhead_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    overhead_camera.keep_aspect = Camera3D.KEEP_HEIGHT
    overhead_camera.size = 15 if stage in ["stage14a","stage15a"] else 28
    root.add_child(overhead_camera)
    overhead_camera.position = Vector3(16,45,17.25) if stage in ["stage14a","stage15a"] else Vector3(16,45,16)
    overhead_camera.look_at(overhead_camera.position + Vector3.DOWN,Vector3.FORWARD)
    overhead_camera.current = true
    for layer in arena.find_children("*","CanvasLayer",true,false):
        layer.hide()
    await process_frame
    await RenderingServer.frame_post_draw
    var overhead = root.get_texture().get_image().save_png("res://artifacts/%s_overhead.png" % stage)
    quit(0 if angled == OK and overhead == OK else 1)
