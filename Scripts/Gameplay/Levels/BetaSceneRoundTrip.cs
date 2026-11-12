using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cell = PawnsOfDivinity.Core.Abilities.GridPosition;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Explicit development smoke check. Writes only a unique temporary variation, never the authored source.</summary>
public static class BetaSceneRoundTrip
{
    public static void Verify(string scenePath)
    {
        using var source=ResourceLoader.Load<PackedScene>(scenePath,"",ResourceLoader.CacheMode.Ignore);
        var clone=source.Instantiate<MobaBetaBattlefield>();
        var before=BetaSceneReader.Extract(clone);
        string path=Path.Combine(Path.GetTempPath(),"PawnsBetaRoundTrip_"+Guid.NewGuid().ToString("N")+".tscn");
        try
        {
            SaveAndCheck(before.Source);
            var tree=clone.GetNode<BetaLevelElement>("Obstacles/tree_3_2");
            tree.Position+=new Vector3(0,0,(float)before.Board.CellSize);
            var edited=BetaSceneReader.Extract(clone);
            Check(edited.Walkable(new Cell(3,2))&&!edited.Walkable(new Cell(3,3)),"Moved obstacle must change static walkability.");
            SaveAndCheck(edited.Source);
            tree.Position+=new Vector3(.1f,0,0);
            bool rejected=false;
            try{BetaSceneReader.Extract(clone);}catch(LevelAuthoringException e){rejected=e.Message.Contains("tree_3_2")&&e.Message.Contains("grid aligned");}
            Check(rejected,"Off-grid placement must identify the offending node.");
            GD.Print("AUTHORED SCENE ROUND TRIP PASSED: native PackedScene pack/save/load, original layout equivalence, moved obstacle persistence/walkability, invalid transform rejection.");
        }
        finally{clone.Free();if(File.Exists(path))File.Delete(path);}

        void SaveAndCheck(AuthoredLevelData expected)
        {
            using var packed=new PackedScene();Check(packed.Pack(clone)==Error.Ok,"Pack authored scene");
            Check(ResourceSaver.Save(packed,path)==Error.Ok,"Save authored variation");
            using var saved=ResourceLoader.Load<PackedScene>(path,"",ResourceLoader.CacheMode.Ignore);
            var restored=saved.Instantiate<MobaBetaBattlefield>();
            try
            {
                var extracted=BetaSceneReader.Extract(restored);
                Check(JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(extracted.Source),"Scene-authored metadata must survive native save/reload unchanged.");
                Check(clone.DevelopmentScoring.Snapshot()==restored.DevelopmentScoring.Snapshot(),"Development scoring resource must survive native save/reload unchanged.");
                Check(clone.DevelopmentTimer.Snapshot()==restored.DevelopmentTimer.Snapshot(),"Development timer resource must survive native save/reload unchanged.");
                Check(clone.DevelopmentAssassin.Snapshot()==restored.DevelopmentAssassin.Snapshot(),"Development Assassin inputs must survive native save/reload unchanged.");
            }
            finally{restored.Free();}
        }
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
}
