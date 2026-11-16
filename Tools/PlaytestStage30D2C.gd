extends SceneTree

var rows=[]
var captured=false

func _initialize():call_deferred("verify")

func cell(x:int,y:int):return Vector3(-2.125+x*1.25,0 if y==14 else -1.8,4.125+y*1.25)

func verify():
    for x in [8,9,20,21]:
        for descending in [true,false]:
            for actor_id in ["hooded_assassin","blue_gravebound_colossus"]:
                var arena=load("res://Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn").instantiate()
                arena.BoardDefinition.RequireDeploymentSpawns=false;arena.FreeTesting=true
                var actor=arena.get_node("CharacterFixtures/"+actor_id);var start=14 if descending else 17;var end=17 if descending else 14
                # Metadata anchors stay at Y0; Ready derives their actual standing height.
                actor.position=Vector3(-2.125+x*1.25,0,4.125+start*1.25)
                root.add_child(arena)
                for i in 12:await process_frame
                arena.call("SelectAt",arena.call("WorldToScreen",cell(x,start)));await process_frame
                arena.call("SelectAt",arena.call("WorldToScreen",cell(x,end)))
                var arrived=false
                for i in 240:
                    await process_frame
                    if not captured and x==8 and descending and actor_id=="blue_gravebound_colossus" and actor.global_position.y<-.4 and actor.global_position.y> -1.3:
                        await RenderingServer.frame_post_draw
                        root.get_texture().get_image().save_png("res://artifacts/stage30d2c/unit_on_stairs.png");captured=true
                    if actor.global_position.distance_to(cell(x,end))<.002:arrived=true;break
                if not arrived:
                    push_error("Animated stair traversal failed: %s x%d %d->%d at%s"%[actor_id,x,start,end,actor.global_position]);quit(1);return
                rows.append({"actor":actor_id,"x":x,"from_row":start,"to_row":end,"arrived":arrived})
                arena.queue_free();await process_frame
    var f=FileAccess.open("res://artifacts/stage30d2c/animated_traversal.json",FileAccess.WRITE);f.store_string(JSON.stringify(rows,"  "));f.close()
    print("FORWARD CASTLE ANIMATED TRAVERSAL PASSED: ",rows.size()," actual floor-click paths, both columns/castles/directions and scout/colossus.")
    quit()
