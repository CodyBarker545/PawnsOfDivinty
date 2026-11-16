using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;

public class TacticalGridOverlayTests
{
    [Theory]
    [InlineData("Scenes/Battlefields/CliffsideArena/CliffsideArena.tscn",false)]
    [InlineData("Scenes/Battlefields/MobaBeta/MobaBetaBattlefield.tscn",true)]
    public async Task NativeGridInputAndPresentationStayIndependent(string scene,bool initiallyVisible)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"PawnsOfDivinity.sln")))root=root.Parent;
        Assert.NotNull(root);
        string? engine=Environment.GetEnvironmentVariable("GODOT_BIN");
        if(string.IsNullOrWhiteSpace(engine))engine=Regex.Match(File.ReadAllText(Path.Combine(root.FullName,".godot/editor/project_metadata.cfg")),"executable_path=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(File.Exists(engine),"Set GODOT_BIN to Godot .NET.");
        string report=Path.Combine(Path.GetTempPath(),"PawnsGrid_"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var start=new ProcessStartInfo(engine){WorkingDirectory=root.FullName,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--headless","--path",root.FullName,scene,"--","--verify-tactical-grid","--grid-report="+report})start.ArgumentList.Add(arg);
            using var process=Process.Start(start)!;
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
            string log=await stdout+await stderr;
            Assert.True(process.ExitCode==0&&File.Exists(report),log);
            Assert.DoesNotContain("ERROR:",log);
            using var json=JsonDocument.Parse(File.ReadAllText(report));var data=json.RootElement;
            Assert.Equal(initiallyVisible,data.GetProperty("defaultVisible").GetBoolean());
            Assert.Equal(!initiallyVisible,data.GetProperty("afterFirstPress").GetBoolean());
            foreach(string check in new[]{"passed","logicalDataIdentical","textInputIgnored","rebindWorks","authoredAlignment","movementIndicatorsIndependent","selectionAndTargeting","cameraInputPreserved"})Assert.True(data.GetProperty(check).GetBoolean(),check);
            Assert.Equal(1,data.GetProperty("gridSurfaces").GetInt32());
        }
        finally{if(File.Exists(report))File.Delete(report);}
    }
}
