extends SceneTree

# Runtime-only review: never saves the arena or changes its authored geometry.

func _initialize():
    call_deferred("capture")

func capture():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    root.add_child(arena)
    for i in 8:
        await process_frame
    arena.process_mode = Node.PROCESS_MODE_DISABLED
    var blocker = arena.find_child("blue_forward_wall_7_8", true, false)
    var art = blocker.get_node("Wall")
    var expected_position = Vector3(-2.125 + 7 * 1.25, 0, 4.125 + 8 * 1.25)
    if not blocker.global_position.is_equal_approx(expected_position) or not art.transform.is_equal_approx(Transform3D.IDENTITY):
        push_error("Wall placement or wrapper transform changed")
        quit(1)
        return
    var bounds = AABB()
    for part in art.find_children("*", "MeshInstance3D", true, false):
        var material = part.get_active_material(0) as StandardMaterial3D
        if not material or not material.albedo_texture or not material.roughness_texture or not material.normal_texture or not material.normal_enabled or abs(material.normal_scale - 0.32) > 0.001 or material.metallic != 0:
            push_error("Missing imported texture or incorrect material baseline")
            quit(1)
            return
        print("Imported material: ",material.resource_name,"; albedo ",material.albedo_texture.get_size(),"; roughness ",material.roughness_texture.get_size(),"; normal ",material.normal_texture.get_size(),"; normal scale ",material.normal_scale,"; metallic ",material.metallic)
        var box = (art.global_transform.affine_inverse() * part.global_transform) * part.mesh.get_aabb()
        bounds = box if bounds.size == Vector3.ZERO else bounds.merge(box)
    print("Imported wall local bounds ",bounds,"; movement ",blocker.BlocksMovement,"; sight ",blocker.BlocksSight)
    if not blocker.BlocksMovement or not blocker.BlocksSight or bounds.size.distance_to(Vector3(1.25,1.085,0.77)) > 0.005 or bounds.position.distance_to(Vector3(-0.625,0,-0.385)) > 0.005:
        quit(1)
        return
    await RenderingServer.frame_post_draw
    var tactical = arena.find_child("Camera3D", true, false) as Camera3D
    tactical.make_current()
    await process_frame
    await RenderingServer.frame_post_draw
    print("Tactical camera size ",tactical.size,"; wall centre on screen ",tactical.unproject_position(art.global_position + Vector3.UP * 0.55))
    if root.get_texture().get_image().save_png("res://artifacts/stage22_tactical.png") != OK:
        quit(1)
        return
    var camera = tactical
    camera.size = 6.0
    var focal = art.global_position + Vector3(0,0.55,0)
    camera.position = focal + Vector3(2.5,4,7.5)
    camera.look_at(focal,Vector3.UP)
    camera.current = true
    await process_frame
    await RenderingServer.frame_post_draw
    var result = camera.get_viewport().get_texture().get_image().save_png("res://artifacts/stage22_close.png")
    print("STAGE 2.2 WALL VERIFICATION ", "PASSED" if result == OK else "FAILED")
    arena.queue_free()
    await process_frame
    quit(0 if result == OK else 1)


