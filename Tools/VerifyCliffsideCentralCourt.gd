extends SceneTree

# Exercise the running battlefield's floor-click handler. Stage the approach in
# memory so the test can start at the walls without playing the opening rounds.
func _initialize():
    call_deferred("verify")

func world_cell(x: int, row: int) -> Vector3:
    return Vector3(16 + (x - 14.5) * 1.25, 0, 16 + (row - 9.5) * 1.25)

func click_cell(arena: Node, x: int, row: int):
    var screen = arena.call("WorldToScreen", world_cell(x, row))
    arena.call("SelectAt", screen)
    await process_frame

func approach(arena: Node, actor: Node3D, start: int, wall: int, goal: int) -> bool:
    await click_cell(arena, start, 11)
    await click_cell(arena, wall, 11)
    if not actor.global_position.is_equal_approx(world_cell(start, 11)):
        return false
    await click_cell(arena, goal, 11)
    # Wait for the actual movement animation to reconcile.
    for frame in 240:
        await process_frame
        if actor.global_position.is_equal_approx(world_cell(goal, 11)):
            return true
    return false

func verify():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    arena.BoardDefinition.RequireDeploymentSpawns = false
    arena.FreeTesting = true
    var blue = arena.get_node("CharacterFixtures/hooded_assassin")
    var red = arena.get_node("CharacterFixtures/nigel")
    blue.position = world_cell(12,11)
    red.position = world_cell(17,11)
    root.add_child(arena)
    for frame in 12:
        await process_frame
    for entry in [[blue,12,13,14],[red,17,16,15]]:
        if not await approach(arena,entry[0],entry[1],entry[2],entry[3]):
            push_error("Central court movement failed for " + str(entry[0].name))
            quit(1)
            return
    await process_frame
    var marker = arena.find_child("RuntimeControl_central_objective",true,false)
    if not marker or not "CONTESTED" in marker.text:
        push_error("Both units arrived but the center did not register as contested.")
        quit(1)
        return
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://artifacts/stage14a_contested.png")
    print("Central court verified: both sides reached the objective around the walls; ", marker.text)
    quit(0 if result == OK else 1)
