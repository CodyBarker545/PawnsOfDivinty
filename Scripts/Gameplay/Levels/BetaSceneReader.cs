using Godot;
using System.Collections.Generic;
using System.Linq;
using Cell = PawnsOfDivinity.Core.Abilities.GridPosition;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Reads native scene nodes without modifying them. Safe before _Ready and on unpacked scenes.</summary>
public static class BetaSceneReader
{
    public static AuthoredLevelData Read(MobaBetaBattlefield root)
    {
        if(root.BoardDefinition is null)throw new LevelAuthoringException(new[]{"MobaBetaBattlefield: assign a BetaBoardDefinition resource."});
        if(!root.Transform.IsEqualApprox(Transform3D.Identity))
            throw new LevelAuthoringException(new[]{"MobaBetaBattlefield: root transform must be identity; move authored child objects instead."});
        var rows=new List<AuthoredElementData>();
        Visit(root,Transform3D.Identity,"","");
        return new(root.BoardDefinition.ToBoard(),root.BoardDefinition.RequireMirroredLayout,rows.ToArray(),root.BoardDefinition.RequireDeploymentSpawns);

        void Visit(Node node,Transform3D parent,string parentPath,string castle)
        {
            string path=parentPath==""?node.Name.ToString():parentPath+"/"+node.Name;
            var transform=node==root?Transform3D.Identity:node is Node3D spatial?parent*spatial.Transform:parent;
            if(node is BetaLevelElement element)
            {
                if(element.Kind==BetaElementKind.Castle)castle=element.ElementId;
                rows.Add(new AuthoredElementData {Path=path,Id=element.ElementId,Kind=element.Kind.ToString(),
                    X=transform.Origin.X,Y=transform.Origin.Y,Z=transform.Origin.Z,
                    HasIdentityBasis=transform.Basis.IsEqualApprox(Basis.Identity)&&!element.TopLevel,
                    CellOffsets=element.CellOffsets.Select(c=>new Cell(c.X,c.Y)).ToArray(),
                    CastleId=element.CastleId!=""?element.CastleId:element.Kind==BetaElementKind.Castle?"":castle,
                    Role=element.Role,TeamId=element.InitialTeamId,IsHome=element.IsHome,
                    BlocksMovement=element.BlocksMovement,TerrainTag=element.TerrainTag});
            }
            if(node is StaticTacticalCharacter fixture)
            {
                rows.Add(new AuthoredElementData {Path=path,Id=fixture.CharacterId,Kind="Fixture",
                    X=transform.Origin.X,Y=transform.Origin.Y,Z=transform.Origin.Z,
                    HasIdentityBasis=transform.Basis.IsEqualApprox(Basis.Identity)&&!fixture.TopLevel,
                    DisplayName=fixture.DisplayName,DefinitionId=fixture.DefinitionId,TeamId=fixture.TeamId,ModelPath=fixture.AuthoredModelPath});
                return; // Imported model internals never become tactical metadata.
            }
            foreach(Node child in node.GetChildren())Visit(child,transform,path,castle);
        }
    }
    public static MobaBetaLayout Extract(MobaBetaBattlefield root) => new(Read(root));
}
