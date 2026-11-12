using System.Text.Json;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class AuthoredLevelValidationTests
{
    private static AuthoredLevelData Data => AuthoredBetaScene.Data;
    private static AuthoredLevelData Edit(string id, Func<AuthoredElementData,AuthoredElementData> change) =>
        Data with {Elements=Data.Elements.Select(e=>e.Id==id ? change(e) : e).ToArray()};
    private static void Reject(AuthoredLevelData data, params string[] messages)
    {
        var error=Assert.Throws<LevelAuthoringException>(()=>new MobaBetaLayout(data));
        foreach(string message in messages)Assert.Contains(message,error.Message);
    }
    private static AuthoredLevelData AddBlocker(AuthoredLevelData data,int x,int y,string id)
    {
        var center=data.Board.CellCenter(new(x,y));
        return data with {Elements=data.Elements.Append(new AuthoredElementData {
            Id=id,Path="Obstacles/"+id,Kind="Obstacle",X=center.X,Z=center.Z,BlocksMovement=true}).ToArray()};
    }

    [Fact] public void SceneExtractionPreservesOriginalGeometryAndRoster()
    {
        var map=new MobaBetaLayout(Data);
        Assert.Equal(600,map.Terrain.Count);Assert.Equal(58,map.Obstacles.Count);
        Assert.Equal(542,map.Board.Cells.Count(map.Walkable));Assert.Equal(10,map.Fixtures.Count);
        Assert.All(map.Fixtures,f=>Assert.StartsWith("res://Assets/Characters/MobaBeta/",f.ModelPath));
        Assert.Equal(6,map.Fixtures.Select(f=>f.ModelPath).Distinct().Count());
    }
    [Fact] public void ExtractedDataSafelyRoundTripsWithoutAnyHardcodedDefaultMap()
    {
        string json=JsonSerializer.Serialize(Data);
        var restored=JsonSerializer.Deserialize<AuthoredLevelData>(json)!;
        var map=new MobaBetaLayout(restored);
        Assert.Equal(json,JsonSerializer.Serialize(map.Source));
        Assert.True(map.Obstacles.SetEquals(new MobaBetaLayout(Data).Obstacles));
    }
    [Fact] public void MovingObstacleChangesOnlyItsBlockedCellAndKeepsItsIdentity()
    {
        var map=new MobaBetaLayout(Edit("tree_3_2",e=>e with {Z=e.Z+Data.Board.CellSize}));
        Assert.True(map.Walkable(new(3,2)));Assert.False(map.Walkable(new(3,3)));
        Assert.Equal(58,map.Obstacles.Count);Assert.Contains(map.Source.Elements,e=>e.Id=="tree_3_2");
    }
    [Fact] public void BlocksMovementIsAuthoritativeRatherThanAnOldObstacleArray()
    {
        var map=new MobaBetaLayout(Edit("tree_3_2",e=>e with {BlocksMovement=false}));
        Assert.True(map.Walkable(new(3,2)));Assert.Equal(57,map.Obstacles.Count);
    }
    [Fact] public void TerrainCanExplicitlyBlockATile()
    {
        var map=new MobaBetaLayout(Edit("tile_3_3",e=>e with {BlocksMovement=true,TerrainTag="authored_rock"}));
        Assert.False(map.Walkable(new(3,3)));Assert.Equal("authored_rock",map.Terrain[new(3,3)]);
        Assert.Equal(59,map.Obstacles.Count);
    }
    [Fact] public void InvalidAlignmentNamesTheNodeAndSuggestedCenter() =>
        Reject(Edit("tree_3_2",e=>e with {X=e.X+.1}),"tree_3_2","grid aligned","Nearest cell");
    [Fact] public void OutOfBoundsObstacleIsRejected() =>
        Reject(Edit("tree_3_2",e=>e with {X=e.X+Data.Board.Width*Data.Board.CellSize}),"tree_3_2","outside board");
    [Fact] public void RotatedMetadataRootsAreRejectedInsteadOfSilentlyReinterpreted() =>
        Reject(Edit("tree_3_2",e=>e with {HasIdentityBasis=false}),"tree_3_2","rotation and scale");
    [Fact] public void DuplicateStableIdentifiersAreRejected() =>
        Reject(Edit("tree_3_2",e=>e with {Id="tree_4_2"}),"globally unique");
    [Fact] public void DuplicateBlockersAreRejectedWithTheirPaths() =>
        Reject(AddBlocker(Data,3,2,"duplicate_tree"),"duplicate blockers","duplicate_tree","tree_3_2");
    [Fact] public void DuplicateTerrainCellsAreRejected() =>
        Reject(Edit("tile_3_3",e=>e with {Z=e.Z-Data.Board.CellSize}),"duplicate floor cells");
    [Fact] public void TerrainHolesAreRejected() =>
        Reject(Data with {Elements=Data.Elements.Where(e=>e.Id!="tile_3_3").ToArray()},"every board cell");
    [Fact] public void BlockedSpawnIdentifiesTheFixture() =>
        Reject(AddBlocker(Data,7,12,"spawn_blocker"),"hooded_assassin","blocked or disconnected");
    [Fact] public void DuplicateSpawnPositionsAreRejected()
    {
        var assassin=Data.Elements.Single(e=>e.Id=="hooded_assassin");
        Reject(Edit("gentle_guardian",e=>e with {X=assassin.X,Z=assassin.Z}),"Overlapping fixtures");
    }
    [Fact] public void MissingOriginalModelFailsRatherThanCreatingAPlaceholder() =>
        Reject(Edit("hooded_assassin",e=>e with {ModelPath=null}),"hooded_assassin","model instance is missing");
    [Fact] public void CaptureDeploymentOverlapIsRejected() =>
        Reject(Edit("blue_home_capture",e=>e with {Z=e.Z+2*Data.Board.CellSize}),"Overlapping zones");
    [Fact] public void UnknownCastleReferenceIsRejected() =>
        Reject(Edit("blue_home_capture",e=>e with {CastleId="missing_castle"}),"does not reference an authored castle");
    [Fact] public void BlockedEntranceIsRejected() =>
        Reject(AddBlocker(Data,6,10,"gate_blocker"),"blue_forward/west","open and accessible");
    [Fact] public void DisconnectedWalkablePocketIsRejected() =>
        Reject(AddBlocker(AddBlocker(Data,0,1,"isolate_south"),1,0,"isolate_east"),"Isolated walkable region");
    [Fact] public void OptionalSymmetryConstraintRejectsAnUnpairedEdit()
    {
        var edited=Edit("tree_3_2",e=>e with {Z=e.Z+Data.Board.CellSize});
        Reject(edited with {RequireMirroredLayout=true},"must be mirrored");
        Assert.Empty(new MobaBetaLayout(Data with {RequireMirroredLayout=true}).Validate());
    }
    [Theory] [InlineData(0,20)] [InlineData(30,0)] [InlineData(257,20)]
    public void InvalidDimensionsFailClearly(int width,int height) =>
        Reject(Data with {Board=Data.Board with {Width=width,Height=height}},"BoardDefinition");
}
