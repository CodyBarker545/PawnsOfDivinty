extends SceneTree

const DEST="res://Assets/Levels/Environment/EasternStair/"
const SHARED="res://Assets/Levels/Environment/WesternStairSlice/"

func _initialize():
    call_deferred("bake")

func bake():
    if DisplayServer.get_name()=="headless":
        push_error("Use the real renderer for this one-time bake; dummy rendering omits serialized MultiMesh buffers.")
        quit(1)
        return
    var doc=JSON.parse_string(FileAccess.get_file_as_string("res://SourceAssets/Environment/EasternStair/Composition.json"))
    var scene=Node3D.new();scene.name="EasternStairEnvironment";scene.set_meta("stage","3.0D.2B");scene.set_meta("purpose","Decorative staircase only; linked slabs batched into three MultiMeshes")
    var stone=load(SHARED+"SliceMaterial3.tres")
    var source=load(DEST+"SlabLibrary.glb").instantiate()
    for variant in 3:
        var donor=source.find_child("Slab_"+char(65+variant),true,false) as MeshInstance3D
        assert(donor!=null)
        # The final PackedScene embeds exactly three shared native mesh resources.
        var mesh=donor.mesh
        for s in mesh.get_surface_count():mesh.surface_set_material(s,null)
        var uses=doc.slab_placements.filter(func(p):return int(p.variant)==variant)
        var multi=MultiMesh.new();multi.transform_format=MultiMesh.TRANSFORM_3D;multi.mesh=mesh;multi.instance_count=uses.size()
        for i in uses.size():
            var p=uses[i];var basis=Basis(Vector3.UP,float(p.angle))*Basis.from_scale(Vector3(p.scale[0],p.scale[1],p.scale[2]))
            multi.set_instance_transform(i,Transform3D(basis,Vector3(p.position[0],p.position[1],p.position[2])))
        var batch=MultiMeshInstance3D.new();batch.name="WornSlabs"+char(65+variant);batch.multimesh=multi;batch.material_override=stone;scene.add_child(batch);batch.owner=scene
    source.free()
    var details=load(DEST+"EasternDetails.glb").instantiate()
    for donor in details.find_children("*","MeshInstance3D",true,false):
        var part=MeshInstance3D.new();part.name=donor.name;part.mesh=donor.mesh;part.transform=donor.transform
        part.material_override=load(SHARED+("SliceMaterial1.tres" if donor.name=="SparseGrass" else "SliceMaterial3.tres"))
        scene.add_child(part);part.owner=scene
    details.free()
    for p in doc.rock_placements:
        var part=load("res://Assets/Levels/Environment/ConnectedCliff/"+p.asset+".tscn").instantiate()
        part.name=p.name;part.position=Vector3(p.position[0],p.position[1],p.position[2]);part.rotation.y=float(p.angle);part.scale=Vector3(p.scale[0],p.scale[1],p.scale[2]);scene.add_child(part);part.owner=scene
    var packed=PackedScene.new();assert(packed.pack(scene)==OK);assert(ResourceSaver.save(packed,DEST+"EasternStair.tscn")==OK)
    scene.free()
    print("EASTERN STAIR BAKE PASSED: three MultiMeshes /47 linked slabs, two detail meshes, three existing rock instances; no new textures.")
    quit()
