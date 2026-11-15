extends "res://Tools/VerifyCliffsideCentralCourt.gd"

func verify():
    var arena = load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
    arena.BoardDefinition.RequireDeploymentSpawns = false
    var scouts = [arena.get_node("CharacterFixtures/hooded_assassin"), arena.get_node("CharacterFixtures/red_hooded_assassin")]
    var tanks = [arena.get_node("CharacterFixtures/blue_gravebound_colossus"), arena.get_node("CharacterFixtures/gravebound_colossus")]
    for side in 2:
        scouts[side].position = world_cell([5,24][side],10)
        tanks[side].position = world_cell([1,28][side],10)
    root.add_child(arena)
    for frame in 12:
        await process_frame
    for side in 2:
        var goal = [8,21][side]
        await click_cell(arena,[5,24][side],10)
        await click_cell(arena,goal,10)
        for frame in 180:
            await process_frame
            if scouts[side].global_position.is_equal_approx(world_cell(goal,10)):
                break
        if not scouts[side].global_position.is_equal_approx(world_cell(goal,10)):
            push_error("Scout failed to enter fort on side %d" % side)
            quit(1)
            return
        for button in arena.find_children("*","Button",true,false):
            if button.text == "End Activation":
                button.emit_signal("pressed")
                break
        await process_frame
    for side in 2:
        await click_cell(arena,[1,28][side],10)
        arena.call("ToggleTransfer")
        arena.call("ConfirmTransfer")
        await process_frame
        var landed = false
        for x in range([7,20][side],[10,23][side]):
            for row in [12,13]:
                if tanks[side].global_position.is_equal_approx(world_cell(x,row)):
                    landed = true
                    print("Forward fort transfer landed on ",Vector2i(x,row))
        if not landed:
            push_error("Transfer failed for side %d" % side)
            quit(1)
            return
    await RenderingServer.frame_post_draw
    var result = root.get_texture().get_image().save_png("res://artifacts/stage15a_landings.png")
    print("Both scouts entered through the gates and both colossi transferred.")
    quit(0 if result == OK else 1)
