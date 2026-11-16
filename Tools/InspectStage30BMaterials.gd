extends SceneTree
func _initialize():
    var art=load("res://Assets/Levels/Environment/WesternStairSlice/WesternStairSlice.glb").instantiate()
    for part in art.find_children("*","MeshInstance3D",true,false):
        for i in part.mesh.get_surface_count():
            var m=part.mesh.surface_get_material(i) as StandardMaterial3D
            var c=part.mesh.surface_get_arrays(i)[Mesh.ARRAY_COLOR]
            print(part.name," ",m.resource_name," vertex ",m.vertex_color_use_as_albedo," srgb ",m.vertex_color_is_srgb," albedo ",m.albedo_color," emissive ",m.emission_enabled," shading ",m.shading_mode," colors ",c[0] if c.size()>0 else "NONE")
    art.free()
    quit()
