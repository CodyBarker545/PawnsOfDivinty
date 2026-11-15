extends SceneTree

const NAMES = ["AssassinTextured", "GentleGuardian", "TimeWeaver", "Nigel", "Colossus", "SpiritGuardian"]
const OUT = "res://artifacts/character_texturing/comparison_renders/"

func _initialize():
    call_deferred("capture")

func capture():
    DirAccess.make_dir_recursive_absolute(OUT)
    var world = Node3D.new()
    root.add_child(world)
    var environment = WorldEnvironment.new()
    environment.environment = Environment.new()
    environment.environment.background_mode = Environment.BG_COLOR
    environment.environment.background_color = Color(0.09, 0.11, 0.14)
    environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
    environment.environment.ambient_light_color = Color.WHITE
    environment.environment.ambient_light_energy = 0.35
    world.add_child(environment)
    var light = DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-40, -30, 0)
    light.light_energy = 0.8
    world.add_child(light)
    var camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.current = true
    world.add_child(camera)
    for character in NAMES:
        if OS.get_cmdline_user_args().size() > 0 and not character in OS.get_cmdline_user_args():
            continue
        for version in ["before", "after"]:
            var path = "res://Assets/Characters/MobaBeta/%s.glb" % character
            if version == "before":
                path = "res://artifacts/character_texturing/backups/audit_baseline_001/Assets/Characters/MobaBeta/%s.glb" % character
            var document = GLTFDocument.new()
            var state = GLTFState.new()
            var error = document.append_from_buffer(FileAccess.get_file_as_bytes(path), "", state)
            assert(error == OK)
            var model = document.generate_scene(state)
            world.add_child(model)
            var skeleton = model.find_children("*", "Skeleton3D", true, false)[0]
            var player = AnimationPlayer.new()
            skeleton.add_child(player)
            player.root_node = NodePath("..")
            player.add_animation_library("", load("res://Assets/Animations/Mixamo/%s.tres" % character))
            player.play("Idle")
            player.advance(0.2)
            player.speed_scale = 0
            var height = 2.69 if character == "Colossus" else 1.7
            var target = Vector3(0, height * 0.5, 0)
            camera.size = height * 1.25
            camera.position = target + Vector3(1.6, 0.6, 5)
            camera.look_at(target)
            await process_frame
            await RenderingServer.frame_post_draw
            root.get_texture().get_image().save_png(OUT + character + "_" + version + ".png")
            print("MATCHED TEXTURE COMPARISON: ", character, " ", version)
            model.queue_free()
            await process_frame
    quit()
