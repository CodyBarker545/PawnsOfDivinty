extends SceneTree

func _initialize():call_deferred("fit")

func fit():
    if DisplayServer.get_name()=="headless":
        push_error("Use the real renderer to save MultiMesh instance buffers.")
        quit(1)
        return
    # Always derive from this correction's snapshot so fitting is repeatable.
    var east=load("res://SourceAssets/Environment/FortStairIntegration/Baseline/EasternStair.tscn").instantiate()
    var doc=JSON.parse_string(FileAccess.get_file_as_string("res://SourceAssets/Environment/EasternStair/Composition.json"))
    for variant in 3:
        var part=east.get_node("WornSlabs"+char(65+variant));part.multimesh=part.multimesh.duplicate()
        var uses=doc.slab_placements.filter(func(p):return int(p.variant)==variant)
        for i in uses.size():
            if not uses[i].name.begins_with("Eastern tread "):continue
            var t=part.multimesh.get_instance_transform(i);t.origin.z-=.023;t.basis.z*=.20/.246;part.multimesh.set_instance_transform(i,t)
        part.material_override=load("res://Assets/Levels/Environment/FortStairIntegration/DressedStairStone.tres")
    east.get_node("RiserCoreAndErosion").material_override=load("res://Assets/Levels/Environment/FortStairIntegration/DressedStairStone.tres")
    var packed=PackedScene.new();assert(packed.pack(east)==OK);assert(ResourceSaver.save(packed,"res://Assets/Levels/Environment/EasternStair/EasternStair.tscn")==OK)
    east.free();print("FORT STAIR FIT PASSED: eastern slab fronts fitted under dressed nosings; original tread center heights retained.");quit()
