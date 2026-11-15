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
    var angled = root.get_texture().get_image().save_png("res://artifacts/stage11_gameplay.png")
    var overhead_camera = Camera3D.new()
    overhead_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    overhead_camera.keep_aspect = Camera3D.KEEP_HEIGHT
    overhead_camera.size = 28
    root.add_child(overhead_camera)
    overhead_camera.position = Vector3(16,45,16)
    overhead_camera.look_at(Vector3(16,0,16),Vector3.FORWARD)
    overhead_camera.current = true
    for layer in arena.find_children("*","CanvasLayer",true,false):
        layer.hide()
    await process_frame
    await RenderingServer.frame_post_draw
    var overhead = root.get_texture().get_image().save_png("res://artifacts/stage11_overhead.png")
    quit(0 if angled == OK and overhead == OK else 1)
