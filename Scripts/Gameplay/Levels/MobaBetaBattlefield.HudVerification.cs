using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using PawnsOfDivinity.Gameplay.Levels;

public partial class MobaBetaBattlefield
{
    private bool _captureHud;
    private void CheckHudGeometry()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("HUD: "+why);}
        var screen=GetViewport().GetVisibleRect();var board=_hud.BattlefieldRect;var bottom=_hud.Bottom.GetGlobalRect();
        Check(board.End.Y<=bottom.Position.Y+.1f&&board.Position.Y>=53,"battlefield is reserved above bottom HUD and below top bar");
        Check(board.Size.Y>screen.Size.Y*.55f,"battlefield occupies the majority of the viewport");
        Check(Math.Abs(_hud.WorldView.Size.X-board.Size.X)<2&&Math.Abs(_hud.WorldView.Size.Y-board.Size.Y)<2,"camera render size matches visible battlefield");
        var buttons=_hud.Commands.GetChildren().OfType<Button>().Where(b=>b.IsVisibleInTree()).ToArray();
        foreach(var b in buttons)Check(bottom.Encloses(b.GetGlobalRect()),$"{b.Text} stays inside bottom HUD");
        for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)Check(!buttons[i].GetGlobalRect().Grow(-1).Intersects(buttons[j].GetGlobalRect()),"action buttons do not overlap");
        Check(screen.Encloses(_hud.InventoryText.GetGlobalRect()),"inventory stays inside viewport");
        foreach(var label in new[]{_hud.NameText,_hud.HealthText,_hud.StatsText,_hud.EffectsText,_hud.Context,_hud.InventoryText})
            if(label.IsVisibleInTree())Check(bottom.Encloses(label.GetGlobalRect()),"readable text stays inside bottom deck: "+label.Text);
        foreach(var label in new[]{_turn,_readiness,_vp,_timerLabel})Check(label.GetGlobalRect().End.Y<=board.Position.Y+.1f,"top text does not cover gameplay");
        Check(!Pick(new Vector2(20,screen.End.Y-20),out _),"bottom HUD is not a tactical picking surface");
        Check(!Pick(new Vector2(20,20),out _),"top bar is not a tactical picking surface");
    }
    private async Task VerifyHudSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("HUD: "+why);}
        ResetMatch();RefreshMatchHud();
        Check(_hud.NameText.Text=="No unit selected"&&_hud.PortraitId is null,"neutral selection and portrait state");
        Check(!_hud.DeveloperPanel.Visible&&!_hud.HelpVisible&&!_combatHud.HistoryExpanded,"utility panels default collapsed");
        Check(_hud.RetainedPortraitCount==_fixtures.Select(f=>f.AuthoredModelPath).Distinct().Count(),"one retained source resource per original model");
        _hud.DeveloperButton.EmitSignal(Button.SignalName.Pressed);Check(_hud.DeveloperPanel.Visible,"developer controls open");
        _hud.DeveloperButton.EmitSignal(Button.SignalName.Pressed);
        _UnhandledInput(new InputEventKey {Keycode=Key.F1,Pressed=true});Check(_hud.HelpVisible,"F1 help opens");
        _hud.HelpButton.EmitSignal(Button.SignalName.Pressed);Check(!_hud.HelpVisible,"help closes");
        _hud.HistoryButton.EmitSignal(Button.SignalName.Pressed); // Toggle buttons change state through GUI, not EmitSignal.
        _combatHud.ToggleHistory();Check(_combatHud.HistoryExpanded,"journal opens from shared toggle");_combatHud.ToggleHistory();
        foreach(string id in new[]{"hooded_assassin","gentle_guardian","blue_time_weaver","beothuk_spirit_guardian","blue_gravebound_colossus"})
        {
            RunCommand(()=>Session.Select(id));Check(_hud.PortraitId==id&&_hud.NameText.Text==Session.Characters[id].Name,"portrait uses stable selected fixture");
            Check(_hud.StatsText.Text.Contains($"STR {Session.Characters[id].BaseStats.Strength}")&&!_hud.StatsText.Text.Contains("Level"),"runtime stats without levels");
            Check(_hud.HealthText.Text.Contains($"{Session.Characters[id].CurrentHealth} / {Session.Characters[id].BaseStats.MaxHealth}"),"HP and shields from runtime");
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect(); // Stress imported-resource lifetime across selection changes.
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        RunCommand(()=>Session.Select("hooded_assassin"));_moveButton.EmitSignal(Button.SignalName.Pressed);
        Check(!_attackMode&&!_transferMode&&!_abilityMode&&!Session.Characters["hooded_assassin"].HasActedThisRound,"Move is existing selection mode, no extra command cost");
        _attack.EmitSignal(Button.SignalName.Pressed);Check(_attackMode&&_confirmAttack.Visible,"attack/confirmation context");
        _moveButton.EmitSignal(Button.SignalName.Pressed);
        var window=GetWindow();var savedSize=window.Size;var scale=window.ContentScaleSize;
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1920,1080)})
        {
            window.ContentScaleSize=Vector2I.Zero;window.Size=size;
            for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            ResetCamera();CheckHudGeometry();
            foreach(var cell in Layout.Board.Cells)Check(Pick(WorldToScreen(CellPosition(cell)),out var picked)&&picked==cell,"resized camera-to-grid round trip");
            _abilitiesButton.EmitSignal(Button.SignalName.Pressed);_previewAbility.EmitSignal(Button.SignalName.Pressed);
            for(int i=0;i<2;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            CheckHudGeometry();Check(_confirmAbility.Visible&&_hud.ContextHost.GetGlobalRect().Encloses(_abilityPanel.GetGlobalRect()),"ability controls stay docked");
            CancelAbilityUi();RefreshMatchHud();
        }
        window.ContentScaleSize=scale;window.Size=savedSize;
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        _resetMatch.EmitSignal(Button.SignalName.Pressed);Check(_hud.PortraitId is null&&_hud.NameText.Text=="No unit selected","reset clears portrait and selected details");
        ResetCamera();GD.Print("TACTICAL HUD VERIFIED: 1280x720 and 1920x1080, portraits, runtime stats, docked commands, utilities, reserved viewport and all-cell picking.");
    }
    private void CaptureTacticalHud()
    {
        string[] names={"empty","selected","abilities","development","help","selected_1080","reset_1080"};
        string path=$"res://artifacts/moba_hud_{names[_captureStep]}.png";GetViewport().GetTexture().GetImage().SavePng(path);GD.Print("HUD CAPTURE: "+path);
        switch(++_captureStep)
        {
            case 1:RunCommand(()=>Session.Select("hooded_assassin"));break;
            case 2:_abilitiesButton.EmitSignal(Button.SignalName.Pressed);_previewAbility.EmitSignal(Button.SignalName.Pressed);break;
            case 3:CancelAbilityUi();RefreshMatchHud();_hud.DeveloperButton.EmitSignal(Button.SignalName.Pressed);break;
            case 4:_hud.DeveloperButton.EmitSignal(Button.SignalName.Pressed);_hud.ToggleHelp();break;
            case 5:_hud.ToggleHelp();GetWindow().ContentScaleSize=Vector2I.Zero;GetWindow().Size=new(1920,1080);ResetCamera();break;
            case 6:ResetMatch();break;
            default:GetTree().Quit();break;
        }
    }
}
