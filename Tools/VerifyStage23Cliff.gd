extends SceneTree

const PAIR = "res://Assets/Levels/Props/ModularCliff/ModularCliff.tscn"
var stage = "stage23a" if "--stage23a" in OS.get_cmdline_user_args() else "stage23b"

func _initialize():
    call_deferred("verify")

func vertices(node: Node3D) -> Array[Vector3]:
    var result: Array[Vector3] = []
    for part in node.find_children("*", "MeshInstance3D", true, false):
        var relative = node.global_transform.affine_inverse() * part.global_transform
        for surface in part.mesh.get_surface_count():
            var arrays = part.mesh.surface_get_arrays(surface)
            for point in arrays[Mesh.ARRAY_VERTEX]:
                result.append(relative * point)
    return result

func bounds(node: Node3D) -> AABB:
    var points = vertices(node)
    var box = AABB(points[0], Vector3.ZERO)
    for point in points:
        box = box.expand(point)
    return box

func section(points: Array[Vector3], axis: int, value: float) -> Array[Vector3]:
    var found: Array[Vector3] = []
    for point in points:
        if abs(point[axis] - value) < 0.0001:
            point[axis] = 0
            if not found.any(func(other): return other.is_equal_approx(point)):
                found.append(point)
    return found

func matches(a: Array[Vector3], b: Array[Vector3]) -> bool:
    # Imported compressed meshes quantize each asset's bounds independently.
    return a.size() == b.size() and a.all(func(point): return b.any(func(other): return other.distance_to(point) < 0.0001))

func geometry(pair: Node3D) -> bool:
    var box = bounds(pair)
    print("Cliff pair bounds ", box)
    if box.position.distance_to(Vector3(-0.625,-1.85,-0.625)) > 0.0001 or box.size.distance_to(Vector3(1.25,1.85,1.25)) > 0.0001:
        push_error("Cliff envelope mismatch")
        return false
    var faces = vertices(pair.get_node("Face"))
    var caps = vertices(pair.get_node("EdgeCap"))
    if not matches(section(faces,1,-0.18),section(caps,1,-0.18)):
        print("Face interface ",section(faces,1,-0.18),"; cap interface ",section(caps,1,-0.18))
        push_error("Face/cap interface mismatch")
        return false
    for asset in pair.get_children():
        var points = vertices(asset)
        if not matches(section(points,0,-0.625),section(points,0,0.625)):
            push_error("Repeated module end profile mismatch")
            return false
        print(asset.name, " local bounds ", bounds(asset), "; end profiles match")
        for mesh in asset.find_children("*", "MeshInstance3D", true, false):
            var triangles = 0
            for surface in mesh.mesh.get_surface_count():
                var arrays = mesh.mesh.surface_get_arrays(surface)
                if arrays[Mesh.ARRAY_TEX_UV].size() != arrays[Mesh.ARRAY_VERTEX].size() or arrays[Mesh.ARRAY_NORMAL].size() != arrays[Mesh.ARRAY_VERTEX].size():
                    push_error("Missing UVs or normals")
                    return false
                triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
                var mat = mesh.get_active_material(surface) as StandardMaterial3D
                if mat == null or mat.metallic != 0:
                    push_error("Incorrect cliff material")
                    return false
                if stage == "stage23b":
                    if not mat.albedo_texture or not mat.roughness_texture or not mat.normal_texture or abs(mat.normal_scale - 0.20) > 0.001:
                        push_error("Missing refined cliff maps or normal strength")
                        return false
                elif abs(mat.roughness - 0.88) > 0.001:
                    push_error("Incorrect original cliff roughness")
                    return false
            print(mesh.name, "; surfaces ",mesh.mesh.get_surface_count(),"; triangles ",triangles)
    if not pair.find_children("*", "CollisionObject3D", true, false).is_empty():
        push_error("Decorative pair introduced collision")
        return false
    print("SEAM AND CAP INTERFACE PASSED: vertices within 0.0001 at 1.25 spacing; UVs/normals/materials present; collision off.")
    return true

func save_view(viewport: Viewport, name: String) -> bool:
    await process_frame
    await RenderingServer.frame_post_draw
    return viewport.get_texture().get_image().save_png("res://artifacts/%s_%s.png" % [stage,name]) == OK

func verify():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    root.add_child(arena)
    for frame in 12:
        await process_frame
    arena.process_mode = Node.PROCESS_MODE_DISABLED
    arena.find_child("RuntimeHover",true,false).hide()
    var tile = arena.get_node("Terrain/tile_14_15")
    var pair = tile.get_node("ModularCliff")
    var old_face = arena.get_node("Terrain/Cliff_14_15")
    print("Retained hidden legacy cliff face bounds ",old_face.global_transform * old_face.get_aabb())
    if old_face.visible:
        push_error("Legacy face still obscures proof module")
        quit(1)
        return
    if tile.global_position.distance_to(Vector3(15.375,0,22.875)) > 0.0001 or not pair.transform.is_equal_approx(Transform3D.IDENTITY) or not tile.BlocksMovement or tile.Elevation != 0 or tile.get_node("Surface").visible:
        push_error("Proof placement/metadata mismatch")
        quit(1)
        return
    print("Proof tile (14,15) world ",tile.global_position,"; movement ",tile.BlocksMovement,"; sight ",tile.BlocksSight)
    print("Main terrace level ",arena.get_node("Terrain/tile_14_14").Elevation,"; farm level ",arena.get_node("Terrain/tile_14_16").Elevation)
    if not geometry(pair):
        quit(1)
        return
    var camera = arena.find_child("Camera3D",true,false) as Camera3D
    print("Normal tactical camera size ",camera.size)
    if not await save_view(root,"tactical"):
        quit(1)
        return
    var focal = tile.global_position + Vector3(0,-0.8,0.3)
    camera.size = 6.5
    camera.position = focal + Vector3(2.5,3.2,7)
    camera.look_at(focal,Vector3.UP)
    if not await save_view(camera.get_viewport(),"close"):
        quit(1)
        return
    arena.queue_free()
    await process_frame

    # Two translated copies, isolated from the battlefield. Neither is saved into it.
    var preview = Node3D.new()
    root.add_child(preview)
    for x in [-0.625,0.625]:
        var instance = load(PAIR).instantiate()
        preview.add_child(instance)
        instance.position.x = x
    var environment = WorldEnvironment.new()
    environment.environment = Environment.new()
    environment.environment.background_mode = Environment.BG_COLOR
    environment.environment.background_color = Color(0.09,0.11,0.14)
    environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    environment.environment.ambient_light_color = Color(0.78,0.86,0.9)
    environment.environment.ambient_light_energy = 0.45
    preview.add_child(environment)
    var light = DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-45,-30,0)
    light.light_energy = 0.85
    preview.add_child(light)
    var seam_camera = Camera3D.new()
    preview.add_child(seam_camera)
    seam_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    seam_camera.keep_aspect = Camera3D.KEEP_HEIGHT
    seam_camera.size = 3.1
    seam_camera.position = Vector3(2.8,1.4,6)
    seam_camera.look_at(Vector3(0,-0.8,0),Vector3.UP)
    seam_camera.current = true
    var okay = await save_view(root,"seam")
    if stage == "stage23b":
        var third = load(PAIR).instantiate()
        preview.add_child(third)
        third.position.x = 1.875
        seam_camera.position.x += 0.625
        seam_camera.look_at(Vector3(0.625,-0.8,0),Vector3.UP)
        seam_camera.size = 3.4
        okay = await save_view(root,"three") and okay
    print(stage.to_upper()," CLIFF VERIFICATION ","PASSED" if okay else "FAILED")
    preview.queue_free()
    await process_frame
    quit(0 if okay else 1)
