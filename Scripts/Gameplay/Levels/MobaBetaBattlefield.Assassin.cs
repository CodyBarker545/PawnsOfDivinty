using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;
using Cell=PawnsOfDivinity.Core.Abilities.GridPosition;

public partial class MobaBetaBattlefield
{
    [Export] public BetaAssassinSettings DevelopmentAssassin {get;set;}=new();
    private Button _abilitiesButton=null!,_previewAbility=null!,_confirmAbility=null!;
    private PanelContainer _abilityPanel=null!;
    private OptionButton _abilityChoices=null!;
    private Label _abilityInfo=null!,_abilityPreview=null!;
    private bool _abilitiesOpen,_abilityMode;
    private string? _abilityTarget;
    private Cell? _abilityDestination;
    private long _abilityRequest;
    private readonly Dictionary<Cell,MeshInstance3D> _smokeTiles=new(),_smokePreviewTiles=new(),_teleportTiles=new();
    private StandardMaterial3D _smokeMaterial=null!,_previewSmokeMaterial=null!,_teleportMaterial=null!;
    private bool _captureAssassin;
    private void BuildAssassinHud(HBoxContainer row,CanvasLayer canvas)
    {
        _abilitiesButton=new Button {Text="Abilities",FocusMode=Control.FocusModeEnum.None};row.AddChild(_abilitiesButton);
        _abilitiesButton.Pressed+=()=>
        {
            if(_abilitiesOpen)CancelAbilityUi();else{_abilitiesOpen=true;_attackMode=false;_transferMode=false;_attackTargetId=null;if(_combatHud.HistoryExpanded)_combatHud.ToggleHistory();}
            RefreshMatchHud();UpdateMovementOverlay(null);
        };
        // Keep targeting controls above projected health labels, with an opaque readable background.
        var targetingCanvas=new CanvasLayer {Layer=2};AddChild(targetingCanvas);
        _abilityPanel=new PanelContainer {Visible=false,AnchorLeft=1,AnchorRight=1,OffsetLeft=-450,OffsetRight=-14,OffsetTop=222};targetingCanvas.AddChild(_abilityPanel);
        _abilityPanel.AddThemeStyleboxOverride("panel",new StyleBoxFlat {BgColor=new(.055f,.07f,.095f),ContentMarginLeft=8,ContentMarginRight=8,ContentMarginTop=8,ContentMarginBottom=8});
        var column=new VBoxContainer {CustomMinimumSize=new(420,0)};_abilityPanel.AddChild(column);
        _abilityInfo=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};_abilityInfo.AddThemeFontSizeOverride("font_size",13);column.AddChild(_abilityInfo);
        _abilityChoices=new OptionButton {FocusMode=Control.FocusModeEnum.None};_abilityChoices.AddItem("Smoke Bomb + in-cloud teleport");_abilityChoices.AddItem("Shadow Strike");column.AddChild(_abilityChoices);
        _abilityChoices.ItemSelected+=_=>{Session.CancelAssassinTargeting();_abilityMode=false;_abilityTarget=null;_abilityDestination=null;RefreshMatchHud();};
        var buttons=new HBoxContainer();column.AddChild(buttons);
        _previewAbility=new Button {Text="Preview Ability",FocusMode=Control.FocusModeEnum.None};buttons.AddChild(_previewAbility);_previewAbility.Pressed+=BeginAbilityUi;
        _confirmAbility=new Button {Text="Confirm Ability",FocusMode=Control.FocusModeEnum.None};buttons.AddChild(_confirmAbility);_confirmAbility.Pressed+=ConfirmAbilityUi;
        var cancel=new Button {Text="Cancel",FocusMode=Control.FocusModeEnum.None};buttons.AddChild(cancel);cancel.Pressed+=()=>{CancelAbilityUi();RefreshMatchHud();UpdateMovementOverlay(null);};
        _abilityPreview=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};_abilityPreview.AddThemeFontSizeOverride("font_size",13);column.AddChild(_abilityPreview);
        _smokeMaterial=Overlay(new(.45f,.55f,.72f,.2f));_previewSmokeMaterial=Overlay(new(.65f,.4f,.95f,.16f));_teleportMaterial=Overlay(new(.1f,.85f,1,.4f));
    }
    private void CancelAbilityUi()
    {_abilitiesOpen=false;_abilityMode=false;_abilityTarget=null;_abilityDestination=null;Session.CancelAssassinTargeting();}
    private void BeginAbilityUi()
    {
        var id=_abilityChoices.Selected==0?HoodedAssassin.SmokeBombId:HoodedAssassin.ShadowStrikeId;
        var result=Session.BeginAssassinAbility(id);_abilityMode=result.Outcome==BattlefieldOutcome.Completed;_message.Text=result.Reason;
        _abilityTarget=null;_abilityDestination=null;RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void ConfirmAbilityUi()
    {
        if(!_abilityMode)return;
        var result=Session.ExecuteAssassinAbility($"ui-assassin-{++_abilityRequest}",_abilityDestination,_abilityTarget);_message.Text=result.Reason;
        if(result.ActorId is { } id)_fixtures.Single(f=>f.CharacterId==id).ReconcileToRuntime(Layout.Board);
        if(result.Combat is not null&&Session.CombatHistory.Records.LastOrDefault() is { } record)
        {_combatClockToken=Session.HoldClockForCombatPresentation(record.RequestId);_combatHud.Present(record);}
        if(result.Outcome==BattlefieldOutcome.Completed)CancelAbilityUi();RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void RefreshAssassinHud()
    {
        bool assassin=Session.SelectedCharacter?.Definition?.Id==HoodedAssassin.Definition.Id;
        _abilitiesButton.Disabled=!assassin||!Session.CanAttack;
        if(!assassin||!Session.CanAttack){_abilitiesOpen=false;_abilityMode=false;_abilityTarget=null;_abilityDestination=null;}
        if(_abilityMode&&Session.TargetedAbilityId is null)_abilityMode=false;
        _abilityPanel.Visible=_abilitiesOpen;
        if(_abilitiesOpen&&_combatHud.HistoryExpanded)_combatHud.ToggleHistory();
        var info=Session.AssassinAbilities();
        _abilityInfo.Text=string.Join("\n",info.Select(a=>$"{a.Name}: {a.Cost}; {(a.ReadyRound is { } r&&r>Session.Round?$"ready R{r}":a.Eligibility)}"))+
            $"\nDEV: unarmed {(Session.AssassinProfile.EnableUnarmedFallback?$"P{Session.AssassinProfile.UnarmedPower}/ACC{Session.AssassinProfile.UnarmedAccuracy}":"unresolved")}; smoke over obstacles {Session.AssassinProfile.SmokeOverObstacles}.";
        _previewAbility.Disabled=!assassin||!Session.CanAttack;
        var preview=_abilityMode?Session.PreviewAssassinAbility(_abilityDestination,_abilityTarget):null;
        _confirmAbility.Disabled=preview?.CanExecute!=true;
        _abilityPreview.Text=preview is null?"Smoke Bomb: fixed 5×5 centered on caster + teleport. Shadow: range 1. Choose Preview; thinking consumes timer time.":
            preview.Reason+(preview.HitChance is { } chance?$"\n{chance}% hit; ESTIMATE {preview.OpeningDamage} opening damage on hit; shield/counters/follow-ups apply.":"");
        if(_abilityMode&&Session.TargetedAbilityId==HoodedAssassin.ShadowStrikeId)
            foreach(var entry in _attackRings)entry.Value.Visible=Session.PreviewAssassinAbility(targetId:entry.Key).CanExecute;
        DrawSmokeTiles(_smokeTiles,Session.Smoke.ActiveAreas.SelectMany(a=>a.Tiles).Distinct(),_smokeMaterial,.055f);
        var footprint=_abilityMode&&Session.TargetedAbilityId==HoodedAssassin.SmokeBombId?preview!.Cells:Array.Empty<Cell>();
        DrawSmokeTiles(_smokePreviewTiles,footprint.Where(Layout.Board.Contains),_previewSmokeMaterial,.06f);
        var destinations=_abilityMode&&Session.TargetedAbilityId==HoodedAssassin.SmokeBombId?
            footprint.Where(c=>Session.PreviewAssassinAbility(c).CanExecute):Enumerable.Empty<Cell>();
        DrawSmokeTiles(_teleportTiles,destinations,_teleportMaterial,.065f);
    }
    private void DrawSmokeTiles(Dictionary<Cell,MeshInstance3D> pool,IEnumerable<Cell> cells,Material material,float height)
    {
        foreach(var tile in pool.Values)tile.Visible=false;
        foreach(var cell in cells)
        {
            if(!pool.TryGetValue(cell,out var tile))
            {
                float scale=pool==_teleportTiles?.65f:.96f;
                tile=new MeshInstance3D {Position=CellPosition(cell,height),Mesh=new BoxMesh {Size=new((float)Layout.Board.CellSize*scale,.008f,(float)Layout.Board.CellSize*scale)},MaterialOverride=material};
                AddChild(tile);pool.Add(cell,tile);
            }
            tile.Visible=true;
        }
    }
    // Explicit native development staging only. None of these methods saves authored fixture transforms.
    private void StageSmokePreview()
    {
        ResetMatch();Session.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));_fixtures.Single(f=>f.CharacterId=="hooded_assassin").ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));_abilitiesButton.EmitSignal(Button.SignalName.Pressed);_abilityChoices.Select(0);_previewAbility.EmitSignal(Button.SignalName.Pressed);
        _abilityDestination=new(13,10);_focus=CellPosition(new(16,10));_camera.Size=18;UpdateCamera();RefreshMatchHud();
    }
    private void StageInvisibleShadowPreview()
    {
        while(Session.Round==1){RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));_endActivation.EmitSignal(Button.SignalName.Pressed);}
        Session.Characters["nigel"].SetTacticalPosition(new(15,10));_fixtures.Single(f=>f.CharacterId=="nigel").ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));MoveAt(new(14,10));AdvanceMovementViews(20);RefreshMatchHud();
        _abilitiesButton.EmitSignal(Button.SignalName.Pressed);_abilityChoices.Select(1);_previewAbility.EmitSignal(Button.SignalName.Pressed);_abilityTarget="nigel";RefreshMatchHud();
    }
    private void VerifyAssassinSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        StageSmokePreview();Check(_abilityPanel.Visible&&!_confirmAbility.Disabled&&Session.Smoke.ActiveAreas.Count==0,"Ability panel and smoke preview are read-only");
        Check(_smokePreviewTiles.Values.Count(t=>t.Visible)==25&&_teleportTiles.Values.Any(t=>t.Visible),"Complete footprint and valid teleport markers");
        Check(Session.ActivationTimer.Pause is not (ActivationTimerPause.Command or ActivationTimerPause.CombatPresentation),"Ability consideration does not pause timer");
        _confirmAbility.EmitSignal(Button.SignalName.Pressed);var assassin=_fixtures.Single(f=>f.CharacterId=="hooded_assassin");
        Check(Session.Characters["hooded_assassin"].TacticalPosition==new Cell(13,10)&&assassin.GlobalPosition==CellPosition(new(13,10)),"Smoke teleport synchronizes domain and original model");
        Check(_smokeTiles.Values.Count(t=>t.Visible)==25&&!assassin.VisibleToObserver&&!_combatHud.HealthVisible(assassin.CharacterId)&&assassin.GetNode<StaticBody3D>("RuntimeSelectionBody").CollisionLayer==0,"Smoke overlay and opponent-hidden model/health/picking");
        Check(Session.Characters["hooded_assassin"].Cooldowns.Cooldowns[HoodedAssassin.SmokeBombId].ReadyRound==4,"Production Smoke Bomb cooldown");
        StageInvisibleShadowPreview();Check(!_confirmAbility.Disabled&&_abilityPreview.Text.Contains("+5")&&_attackRings["nigel"].Visible,"Pre-approach Shadow preview and legal target ring");
        _confirmAbility.EmitSignal(Button.SignalName.Pressed);
        Check(Session.Characters["nigel"].Statuses.HasStatus(PawnsOfDivinity.Core.Statuses.StatusType.Disarm)&&_message.Text.Contains("Disarm True"),"Actual production Disarm and combat feedback");
        Check(assassin.VisibleToObserver&&Session.Characters[assassin.CharacterId].SmokeReveal.RevealedByAttack&&Session.PendingCombatClockPresentation is not null,"Attack reveal and committed combat playback pause");
        int hp=Session.Characters["nigel"].CurrentHealth;_confirmAbility.EmitSignal(Button.SignalName.Pressed);Check(Session.Characters["nigel"].CurrentHealth==hp,"Repeated ability confirmation cannot deal damage again");
        _combatHud._Process(100);AdvanceActivationClock();
        while(Session.Round==2){RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));_endActivation.EmitSignal(Button.SignalName.Pressed);}
        Check(Session.Smoke.ActiveAreas.Count==0&&_smokeTiles.Values.All(t=>!t.Visible),"Production cleanup expires cloud and visual overlay");
        var pose=(_focus,_yaw,_camera.Size);_resetMatch.EmitSignal(Button.SignalName.Pressed);
        Check(Session.Smoke.ActiveAreas.Count==0&&Session.Characters.Values.All(c=>!c.SmokeReveal.RevealedByAttack&&c.Cooldowns.Cooldowns.Count==0)&&pose==(_focus,_yaw,_camera.Size),"Reset clears ability effects and preserves camera");
        var clock=UseTimerSmokeClock();RunCommand(()=>Session.Select("hooded_assassin"));_abilitiesButton.EmitSignal(Button.SignalName.Pressed);_abilityChoices.Select(0);_previewAbility.EmitSignal(Button.SignalName.Pressed);
        _pendingSelection=new Vector2(4,4);clock.NowSeconds+=Session.ActivationTimer.Configuration.DurationSeconds;AdvanceActivationClock();RefreshMatchHud();
        Check(!_abilityPanel.Visible&&!_abilityMode&&_pendingSelection is null&&Session.Smoke.ActiveAreas.Count==0,"Timeout cancels unconfirmed ability UI without smoke/cooldown");
        Session=Session.CreateResetSession(timeSource:new MonotonicActivationTimeSource());ResetMatch();
    }
}
