using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MobaBetaBattlefield
{
    private const string TacticalGridAction="toggle_tactical_grid";
    private const float TacticalGridLift=.025f;
    private MeshInstance3D? _batchedTacticalGrid;
    private int _legacyGridMeshCount;
    private int _batchedGridVertexCount;
    private bool _tacticalGridVerificationStarted;

    private bool TacticalTextInputFocused()
    {
        // HUD controls can belong to the root viewport while the world has its own SubViewport.
        return GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit
            || _hud?.WorldView.GuiGetFocusOwner() is LineEdit or TextEdit;
    }

    private void InitializeTacticalGrid()
    {
        _grid.Visible=TacticalGridInitiallyVisible;
        var authored=_grid.GetChildren().OfType<MeshInstance3D>().ToArray();
        _legacyGridMeshCount=authored.Length;
        // The original beta already has one shared line mesh. Retain its established rendering.
        if(authored.Length<=1)return;
        var vertices=new List<Vector3>();var normals=new List<Vector3>();
        Material? material=null;
        foreach(var part in authored)
        {
            var relative=_grid.GlobalTransform.AffineInverse()*part.GlobalTransform;
            var normalBasis=relative.Basis.Inverse().Transposed();
            for(int surface=0;surface<part.Mesh.GetSurfaceCount();surface++)
            {
                if(part.Mesh is ArrayMesh array)
                {if(array.SurfaceGetPrimitiveType(surface)!=Mesh.PrimitiveType.Triangles)return;}
                else if(part.Mesh is not BoxMesh)return;
                var current=part.GetActiveMaterial(surface);
                if(material is null)material=current;
                else if(current!=material)return; // Never silently merge differing authored styles.
                var arrays=part.Mesh.SurfaceGetArrays(surface);
                var points=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var sourceNormals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                var indices=arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                var order=indices.Length>0?indices:Enumerable.Range(0,points.Length).ToArray();
                foreach(int index in order)
                {
                    vertices.Add(relative*points[index]+Vector3.Up*TacticalGridLift);
                    normals.Add(sourceNormals.Length>index?(normalBasis*sourceNormals[index]).Normalized():Vector3.Up);
                }
            }
        }
        var packed=new Godot.Collections.Array();packed.Resize((int)Mesh.ArrayType.Max);
        packed[(int)Mesh.ArrayType.Vertex]=vertices.ToArray();packed[(int)Mesh.ArrayType.Normal]=normals.ToArray();
        var mesh=new ArrayMesh();mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,packed);
        _batchedTacticalGrid=new MeshInstance3D {Name="TacticalGridLines",Mesh=mesh,MaterialOverride=material,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        _grid.AddChild(_batchedTacticalGrid);_batchedGridVertexCount=vertices.Count;
        foreach(var part in authored)part.Visible=false;
    }
}
