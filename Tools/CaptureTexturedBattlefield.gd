extends SceneTree

# Read-only visual verification: instantiate the real battlefield and aim a
# temporary camera at its existing fixtures. No scene or gameplay data is saved.
func _initialize():
    call_deferred("capture")

func capture():
    var battlefield = load("res://Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn").instantiate()
    root.add_child(battlefield)
    await process_frame
    await process_frame
    battlefield.process_mode = Node.PROCESS_MODE_DISABLED
    for layer in battlefield.find_children("*", "CanvasLayer", true, false):
        layer.hide()
    var camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.size = 3.8
    root.add_child(camera)
    camera.current = true
    var found = {}
    for node in battlefield.find_children("*", "Node3D", true, false):
        var script = node.get_script()
        if script == null or not script.resource_path.ends_with("StaticTacticalCharacter.cs"):
            continue
        var model = node.get_node_or_null(NodePath("Facing/Model"))
        if model == null:
            for child in node.find_children("*", "Node3D", true, false):
                if child.scene_file_path.ends_with(".glb"):
                    model = child
                    break
        if model == null:
            continue
        var character = model.scene_file_path.get_file().get_basename()
        if found.has(character):
            continue
        found[character] = true
        model.show()
        var target = node.global_position + Vector3(0, 1.0, 0)
        if character == "Colossus":
            target.y += 0.4
            camera.size = 4.8
        else:
            camera.size = 3.8
        camera.global_position = target + node.get_node("Facing").global_basis * Vector3(2, 1.4, 4)
        camera.look_at(target)
        await process_frame
        await RenderingServer.frame_post_draw
        var output = "res://artifacts/character_texturing/battlefield_%s.png" % character
        root.get_texture().get_image().save_png(output)
        print("BATTLEFIELD MATERIAL CAPTURE: ", character, " fixture=", node.get_path())
    print("BATTLEFIELD MATERIAL CAPTURE COUNT: ", found.size())
    quit(0 if found.size() == 6 else 1)
