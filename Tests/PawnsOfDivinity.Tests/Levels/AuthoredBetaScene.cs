using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using PawnsOfDivinity.Gameplay.Levels;

namespace PawnsOfDivinity.Tests.Levels;

/// <summary>One headless Godot run per test process extracts the real serialized scene. No copied map fixture.</summary>
internal static class AuthoredBetaScene
{
    private static readonly Lazy<AuthoredLevelData> Snapshot=new(Extract);
    public static AuthoredLevelData Data=>Snapshot.Value;
    public static MobaBetaLayout Layout=>new(Data);
    // Explicit historical 3v3 adapter fixtures retain the B.1/B.2 regression scenarios while
    // all terrain/model/zone data still comes from the native scene. The live map uses Layout.
    public static AuthoredLevelData LegacyData
    {
        get
        {
            var cells=new Dictionary<string,PawnsOfDivinity.Core.Abilities.GridPosition>{
                ["hooded_assassin"]=new(2,10),["gentle_guardian"]=new(1,12),["beothuk_spirit_guardian"]=new(3,12),
                ["nigel"]=new(27,10),["time_weaver"]=new(28,12),["gravebound_colossus"]=new(26,12)};
            var data=Data;
            return data with {RequireDeploymentSpawns=false,Elements=data.Elements.Where(e=>e.Kind!="Fixture"||cells.ContainsKey(e.Id)).Select(e=>
            {if(e.Kind!="Fixture")return e;var p=data.Board.CellCenter(cells[e.Id]);return e with {X=p.X,Z=p.Z};}).ToArray()};
        }
    }
    public static MobaBetaLayout LegacyLayout=>new(LegacyData);
    private static AuthoredLevelData Extract()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"PawnsOfDivinity.sln")))root=root.Parent;
        if(root is null)throw new InvalidOperationException("Cannot locate the Godot project root.");
        string? engine=Environment.GetEnvironmentVariable("GODOT_BIN");
        string metadata=Path.Combine(root.FullName,".godot/editor/project_metadata.cfg");
        if(string.IsNullOrWhiteSpace(engine)&&File.Exists(metadata))
            engine=Regex.Match(File.ReadAllText(metadata),"executable_path=\"([^\"]+)\"").Groups[1].Value;
        if(string.IsNullOrWhiteSpace(engine)||!File.Exists(engine))
            throw new InvalidOperationException("Authored scene tests require Godot 4.7 .NET. Set GODOT_BIN to its executable path, or open the project once in the editor.");
        string temp=Path.Combine(Path.GetTempPath(),"PawnsBetaTests_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            string output=Path.Combine(temp,"layout.json");
            var start=new ProcessStartInfo(engine){WorkingDirectory=root.FullName,UseShellExecute=false,
                CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--headless","--path",root.FullName,"--log-file",Path.Combine(temp,"godot.log"),
                "Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn","--","--export-beta-layout="+output})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new InvalidOperationException("Godot did not start.");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(45000)){process.Kill(true);throw new InvalidOperationException("Godot authored scene extraction timed out.");}
            string log=stdout.GetAwaiter().GetResult()+stderr.GetAwaiter().GetResult();
            if(process.ExitCode!=0||!File.Exists(output)||log.Contains("ERROR:"))throw new InvalidOperationException("Godot authored scene validation failed:\n"+log);
            return JsonSerializer.Deserialize<AuthoredLevelData>(File.ReadAllText(output))??throw new InvalidOperationException("Empty authored scene snapshot.");
        }
        finally{Directory.Delete(temp,true);}
    }
}
