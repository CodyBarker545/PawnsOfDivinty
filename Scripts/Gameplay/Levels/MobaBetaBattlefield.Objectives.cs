using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;

public partial class MobaBetaBattlefield
{
    [Export] public BetaScoringSettings DevelopmentScoring {get;set;}=new();
    private Label _vp=null!,_territoryNotice=null!,_winnerText=null!;
    private PanelContainer _victoryPanel=null!;
    private Button _transfer=null!,_confirmTransfer=null!;
    private OptionButton _transferChoices=null!;
    private readonly List<CastleTransferPlan> _transferPlans=new();
    private readonly Dictionary<string,Label3D> _zoneMarkers=new();
    private bool _transferMode;
    private long _transferSequence;
    private bool _captureObjectives;

    private void BuildTerritoryHud(CanvasLayer canvas,VBoxContainer top,VBoxContainer bottom,HBoxContainer buttons)
    {
        _vp=new Label();top.AddChild(_vp);
        _territoryNotice=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};_territoryNotice.AddThemeFontSizeOverride("font_size",14);bottom.AddChild(_territoryNotice);
        _transfer=new Button {Text="Castle Transfer",FocusMode=Control.FocusModeEnum.None};buttons.AddChild(_transfer);_transfer.Pressed+=ToggleTransfer;
        _transferChoices=new OptionButton {CustomMinimumSize=new(170,0),FocusMode=Control.FocusModeEnum.None};buttons.AddChild(_transferChoices);
        _confirmTransfer=new Button {Text="Confirm Transfer",FocusMode=Control.FocusModeEnum.None};buttons.AddChild(_confirmTransfer);_confirmTransfer.Pressed+=ConfirmTransfer;
        _transferChoices.ItemSelected+=_=>_confirmTransfer.Disabled=!_transferMode||_transferChoices.Selected<0;
        foreach(var zone in Session.Control.AllZones())
        {
            var castle=Layout.Castles.SingleOrDefault(c=>c.Id==zone.Id);var cells=castle?.CaptureCells??Layout.Objective;
            var center=cells.Select(c=>CellPosition(c)).Aggregate(Vector3.Zero,(a,b)=>a+b)/cells.Count;
            // Banners sit toward the south edge, clear of capture occupants' health/nameplates.
            var banner=castle is null?center+new Vector3(0,.6f,(float)Layout.Board.CellSize*2)
                :new Vector3(center.X,1.3f,CellPosition(new(0,castle.Footprint.Max(p=>p.Y))).Z);
            var marker=new Label3D {Name="RuntimeControl_"+zone.Id,Position=banner,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest=true,FontSize=30,PixelSize=.009f,OutlineSize=8};AddChild(marker);_zoneMarkers.Add(zone.Id,marker);
        }
        foreach(var label in GetNode("Objectives").FindChildren("*","Label3D",true,false).OfType<Label3D>())
            if(label.Text.Contains("INACTIVE",StringComparison.OrdinalIgnoreCase))label.Visible=false;
        var victoryCanvas=new CanvasLayer {Layer=20};AddChild(victoryCanvas);
        _victoryPanel=new PanelContainer {Visible=false,AnchorLeft=.5f,AnchorRight=.5f,AnchorTop=.5f,AnchorBottom=.5f,OffsetLeft=-240,OffsetRight=240,OffsetTop=-100,OffsetBottom=100};victoryCanvas.AddChild(_victoryPanel);
        var column=new VBoxContainer();_victoryPanel.AddChild(column);_winnerText=new Label {HorizontalAlignment=HorizontalAlignment.Center};_winnerText.AddThemeFontSizeOverride("font_size",24);column.AddChild(_winnerText);
        var reset=new Button {Text="Reset Match (DEV)",FocusMode=Control.FocusModeEnum.None};reset.Pressed+=ResetMatch;column.AddChild(reset);
    }
    private void RefreshTerritoryHud()
    {
        var v=Session.Victory;var p=v.Profile;
        _vp.Text=$"BLUE {v.BlueVP}/15 VP (+{v.Pending(1)} pending) · RED {v.RedVP}/15 VP (+{v.Pending(2)} pending) · DEV K{p.DefeatPoints}/C{p.CastleCapturePoints}/F{p.ForwardCastleHoldPoints}/O{p.CentralHoldPoints} · round-end awards";
        _vp.TooltipText=p.Label+". Central control requires living uncontested occupation (development policy).";
        foreach(var zone in Session.Control.AllZones())
        {
            string owner=zone.OwnerTeamId==1?"BLUE":zone.OwnerTeamId==2?"RED":"NEUTRAL";
            _zoneMarkers[zone.Id].Text=$"{zone.Id}\n{owner}{(zone.IsHome?" HOME (fixed)":"")}{(zone.Contested?" · CONTESTED":"")}";
            _zoneMarkers[zone.Id].Modulate=zone.Contested?Colors.Orange:zone.OwnerTeamId==1?new(.3f,.8f,1):zone.OwnerTeamId==2?new(1,.4f,.4f):Colors.White;
        }
        _territoryNotice.Text=Session.TerritoryHistory.Count>0?string.Join(" · ",Session.TerritoryHistory.TakeLast(2)):"Capture: finish movement in an uncontested forward zone. DEV objective/occupied fort VP settle at round end.";
        _victoryPanel.Visible=v.IsFinished;
        _winnerText.Text=(v.WinnerTeamId is { } winner?$"PLAYER {winner} WINS":"DRAW — DEV TIE POLICY")+$"\nBlue {v.BlueVP} · Red {v.RedVP}\nRound {Session.Round} · match finished";
        if(!Session.CanAttack)_transferMode=false;
        _transfer.Text=_transferMode?"Cancel Transfer":"Castle Transfer";
        _transfer.Disabled=!_transferMode&&!Session.CanBeginTransfer;
        var plans=new List<CastleTransferPlan>();
        if(_transferMode)foreach(var castle in Layout.Castles)foreach(var cell in castle.DeploymentCells.OrderBy(c=>c.X).ThenBy(c=>c.Y))
        {var preview=Session.PreviewTransfer(castle.Id,cell);if(preview.CanExecute)plans.Add(preview.Plan!);}
        if(!_transferPlans.SequenceEqual(plans))
        {
            var previous=_transferChoices.Selected>=0&&_transferChoices.Selected<_transferPlans.Count?_transferPlans[_transferChoices.Selected]:null;
            _transferPlans.Clear();_transferPlans.AddRange(plans);_transferChoices.Clear();
            foreach(var plan in plans)_transferChoices.AddItem($"{plan.DestinationCastleId} ({plan.DeploymentCell.X},{plan.DeploymentCell.Y})");
            if(plans.Count>0)_transferChoices.Select(Math.Max(0,previous is null?-1:plans.IndexOf(previous)));
        }
        _transferChoices.Disabled=!_transferMode||plans.Count==0;_confirmTransfer.Disabled=_transferChoices.Disabled||_transferChoices.Selected<0;
    }
    private void ToggleTransfer()
    {
        CancelAbilityUi();
        if(_transferMode){_transferMode=false;_message.Text="Transfer cancelled; Main Action remains.";}
        else{var result=Session.BeginTransferTargeting();_message.Text=result.Reason;_transferMode=result.Outcome==BattlefieldOutcome.Completed;_attackMode=false;_attackTargetId=null;}
        RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void ConfirmTransfer()
    {
        if(!_transferMode||_transferChoices.Selected<0||_transferChoices.Selected>=_transferPlans.Count)return;
        var plan=_transferPlans[_transferChoices.Selected];var result=Session.ExecuteTransfer($"ui-transfer-{++_transferSequence}",plan);_message.Text=result.Reason;
        if(result.Outcome is BattlefieldOutcome.Completed or BattlefieldOutcome.PartiallyApplied)_fixtures.Single(f=>f.CharacterId==plan.ActorId).ReconcileToRuntime(Layout.Board);
        if(result.Outcome==BattlefieldOutcome.Completed)_transferMode=false;
        RefreshMatchHud();UpdateMovementOverlay(null);
    }
    // Development-only native checks/captures stage positions in memory; never save the source scene.
    private void StageCastleTransferDemonstration()
    {
        ResetMatch();Session.Characters["hooded_assassin"].SetTacticalPosition(new(19,10));
        _fixtures.Single(f=>f.CharacterId=="hooded_assassin").ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));MoveAt(new(20,10));AdvanceMovementViews(20);RefreshMatchHud();
        _focus=CellPosition(new(20,10));_camera.Size=17;UpdateCamera();
    }
    private void StageObjectiveVictoryDemonstration()
    {
        ResetMatch();RunCommand(()=>Session.Select("gentle_guardian"));MoveAt(new(8,11));AdvanceMovementViews(20);RefreshMatchHud();_endActivation.EmitSignal(Button.SignalName.Pressed);
        RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));_endActivation.EmitSignal(Button.SignalName.Pressed);
        Session.Characters["hooded_assassin"].SetTacticalPosition(new(14,9));_fixtures.Single(f=>f.CharacterId=="hooded_assassin").ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));MoveAt(new(14,10));AdvanceMovementViews(20);RefreshMatchHud();_endActivation.EmitSignal(Button.SignalName.Pressed);
        for(int commands=0;!Session.Victory.IsFinished&&commands<500;commands++)
        {RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));_endActivation.EmitSignal(Button.SignalName.Pressed);}
        if(!Session.Victory.IsFinished)throw new InvalidOperationException("Victory demo requires a development profile with positive occupied-fort/central round awards.");
        ResetCamera();RefreshMatchHud();
    }
    private void StageContestedCaptureDemonstration()
    {
        ResetMatch();Session.Characters["hooded_assassin"].SetTacticalPosition(new(19,10));Session.Characters["nigel"].SetTacticalPosition(new(21,10));
        foreach(string id in new[]{"hooded_assassin","nigel"})_fixtures.Single(f=>f.CharacterId==id).ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));MoveAt(new(20,10));AdvanceMovementViews(20);RefreshMatchHud();
        _focus=CellPosition(new(20,10));_camera.Size=17;UpdateCamera();
    }
    private void CompleteContestedCaptureDemonstration()
    {
        var target=Session.Characters["nigel"];target.TakeDamage(target.CurrentHealth-1);ToggleAttackMode();_attackTargetId=target.Id;RefreshMatchHud();_confirmAttack.EmitSignal(Button.SignalName.Pressed);
    }
    private void VerifyObjectiveSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        StageCastleTransferDemonstration();Check(Session.Control.Castle("red_forward").OwnerTeamId==1&&_zoneMarkers["red_forward"].Text.Contains("BLUE"),"Movement captures castle and updates owner marker");
        Check(_territoryNotice.Text.Contains("captured red_forward"),"Capture notification");
        ToggleTransfer();int index=_transferPlans.FindIndex(p=>p.DestinationCastleId=="blue_home"&&p.DeploymentCell==new PawnsOfDivinity.Core.Abilities.GridPosition(1,12));
        Check(index>=0,"Friendly castle transfer offers unoccupied authored deployment choices");_transferChoices.Select(index);_confirmTransfer.EmitSignal(Button.SignalName.Pressed);
        Check(Session.Characters["hooded_assassin"].TacticalPosition==new PawnsOfDivinity.Core.Abilities.GridPosition(1,12)&&_fixtures.Single(f=>f.CharacterId=="hooded_assassin").GlobalPosition==CellPosition(new(1,12)),"Transfer synchronizes authoritative position and visual");
        Check(Session.CurrentPlayer==2&&Session.Characters["hooded_assassin"].HasActedThisRound,"Transfer consumes Main Action and ends activation");
        StageContestedCaptureDemonstration();Check(_zoneMarkers["red_forward"].Text.Contains("CONTESTED")&&_transfer.Disabled&&Session.Control.Castle("red_forward").OwnerTeamId==2,"Contested feedback blocks transfer without changing owner");
        CompleteContestedCaptureDemonstration();Check(Session.Control.Castle("red_forward").OwnerTeamId==1&&!Session.Control.Castle("red_forward").Contested&&_territoryNotice.Text.Contains("captured red_forward"),"Final defender defeat immediately captures and notifies");
        StageObjectiveVictoryDemonstration();Check(Session.Victory.WinnerTeamId==1&&_victoryPanel.Visible&&_winnerText.Text.Contains("WINS"),"End-round objective scoring presents victory overlay");
        Check(_vp.Text.Contains($"BLUE {Session.Victory.BlueVP}/15 VP")&&_endActivation.Disabled&&_attack.Disabled&&_transfer.Disabled,"Final VP HUD and gameplay locks");
        Check(Session.MoveTo(new(14,9)).Outcome==PawnsOfDivinity.MovementOutcome.Rejected,"Movement rejected after victory");
        var pose=(_focus,_yaw,_camera.Size);int markers=_zoneMarkers.Count;_resetMatch.EmitSignal(Button.SignalName.Pressed);
        Check(!Session.Victory.IsFinished&&Session.Victory.BlueVP==0&&Session.Victory.RedVP==0&&!_victoryPanel.Visible,"Reset clears victory and VP");
        Check(Session.Control.Castle("red_forward").OwnerTeamId==2&&Session.Control.Objective.OwnerTeamId is null&&_zoneMarkers.Count==markers&&(_focus,_yaw,_camera.Size)==pose,"Reset restores ownership/neutral center, preserves camera and reuses marker nodes");
    }
}
