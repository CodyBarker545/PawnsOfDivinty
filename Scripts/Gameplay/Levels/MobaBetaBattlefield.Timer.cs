using Godot;
using System;
using System.Linq;
using PawnsOfDivinity.Gameplay;
using PawnsOfDivinity.Gameplay.Levels;

public partial class MobaBetaBattlefield
{
    [Export] public BetaActivationTimerSettings DevelopmentTimer {get;set;}=new();
    private Label _timerLabel=null!;
    private CheckBox _timerEnabled=null!;
    private Button _timerPause=null!;
    private long _seenTimeout;
    private CombatClockPresentation? _combatClockToken;
    private sealed class SmokeTime : IActivationTimeSource {public double NowSeconds {get;set;}}
    private SmokeTime? _captureTime;
    private bool _captureTimer;
    private void BuildTimerHud(HBoxContainer row)
    {
        _timerLabel=new Label();row.AddChild(_timerLabel);
        _timerEnabled=new CheckBox {Text="Timer (DEV)",FocusMode=Control.FocusModeEnum.None};row.AddChild(_timerEnabled);
        _timerEnabled.Toggled+=enabled=>{_message.Text=Session.SetDevelopmentTimerEnabled(enabled).Reason;RefreshMatchHud();UpdateMovementOverlay(null);};
        _timerPause=new Button {Text="Pause (DEV)",FocusMode=Control.FocusModeEnum.None};row.AddChild(_timerPause);
        _timerPause.Pressed+=()=>{_message.Text=Session.SetDevelopmentTimerPaused(!Session.ActivationTimer.DevelopmentPaused).Reason;RefreshMatchHud();UpdateMovementOverlay(null);};
    }
    private void RefreshTimerHud()
    {
        if(_seenTimeout!=Session.TimeoutCount)
        {
            _seenTimeout=Session.TimeoutCount;_pendingSelection=null;_attackMode=false;_attackTargetId=null;_transferMode=false;_hoverPlan=null;
            CancelAbilityUi();
            _message.Text=Session.LastTimeoutMessage;
        }
        var timer=Session.ActivationTimer;
        string state=timer.Stopped?"STOPPED":!timer.Enabled?"UNTIMED":$"{System.Math.Ceiling(timer.RemainingSeconds):0}s"+(timer.Pause!=ActivationTimerPause.None?$" [PAUSED {timer.Pause}]":"");
        _timerLabel.Text=$" · {(timer.PlayerId is { } p?$"P{p}":"—")} {state}";
        _timerLabel.TooltipText=$"DEV activation clock: {timer.RemainingSeconds:F2}/{timer.Configuration.DurationSeconds:0.##} seconds. Selection and targeting consume the same allowance. {Session.LastTimeoutMessage}";
        _timerLabel.Modulate=timer.IsUrgent?Colors.OrangeRed:timer.Pause!=ActivationTimerPause.None?Colors.LightGray:Colors.White;
        _timerEnabled.SetPressedNoSignal(timer.Enabled);_timerEnabled.Disabled=timer.Stopped;
        _timerPause.Text=timer.DevelopmentPaused?"Resume (DEV)":"Pause (DEV)";_timerPause.Disabled=!timer.Enabled||timer.Stopped;
    }
    private void AdvanceActivationClock()
    {
        if(_combatClockToken is { } token&&_combatHud.FloatingCount==0)
        {Session.AcknowledgeCombatClockPresentation(token);_combatClockToken=null;}
        Session.UpdateActivationTimer();
    }
    private SmokeTime UseTimerSmokeClock()
    {
        var clock=new SmokeTime();Session=Session.CreateResetSession(timeSource:clock);ResetMatch();Session.SetDevelopmentTimerEnabled(true);RefreshMatchHud();return clock;
    }
    private void VerifyTimerSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        var clock=UseTimerSmokeClock();double duration=Session.ActivationTimer.Configuration.DurationSeconds;
        void Step(double seconds){clock.NowSeconds+=seconds;AdvanceActivationClock();RefreshMatchHud();UpdateMovementOverlay(null);}
        Check(Session.ActivationTimer.PlayerId==1&&Math.Abs(Session.ActivationTimer.RemainingSeconds-duration)<.001,"Clock starts before selection");
        Step(duration*.2);RunCommand(()=>Session.Select("hooded_assassin"));Step(duration*.1);RunCommand(()=>Session.Select("gentle_guardian"));
        Check(Math.Abs(Session.ActivationTimer.RemainingSeconds-duration*.7)<.001,"Selection changes preserve remaining allowance");
        _pendingSelection=new Vector2(5,5);Step(duration*.7+.01);
        Check(Session.CurrentPlayer==2&&Session.Characters["gentle_guardian"].HasActedThisRound&&_pendingSelection is null,"Timeout forfeits selection and clears queued click");
        _timerPause.EmitSignal(Button.SignalName.Pressed);Step(100);Check(Session.ActivationTimer.DevelopmentPaused&&_timerLabel.Text.Contains("PAUSED"),"DEV pause control");
        _timerPause.EmitSignal(Button.SignalName.Pressed);_timerEnabled.ButtonPressed=false;Step(100);
        Check(!Session.ActivationTimer.Enabled&&_timerLabel.Text.Contains("UNTIMED")&&Session.TimeoutCount==1,"DEV untimed hotseat checkbox");
        _timerEnabled.ButtonPressed=true;
        RunCommand(()=>Session.Select("nigel"));MoveAt(Session.ReachableDestinations().First().Key);double before=Session.ActivationTimer.RemainingSeconds;
        Step(100);Check(Session.ActivationTimer.RemainingSeconds==before&&Session.IsPresentationBusy,"Movement playback does not waste countdown");
        AdvanceMovementViews(20);RefreshMatchHud();_endActivation.EmitSignal(Button.SignalName.Pressed);
        Session.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));Session.Characters["nigel"].SetTacticalPosition(new(15,10));
        foreach(var id in new[]{"hooded_assassin","nigel"})_fixtures.Single(f=>f.CharacterId==id).ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));ToggleAttackMode();_attackTargetId="nigel";RefreshMatchHud();int hp=Session.Characters["nigel"].CurrentHealth;
        Step(duration);Check(!_attackMode&&_attackTargetId is null&&Session.Characters["nigel"].CurrentHealth==hp,"Timeout cancels unconfirmed attack without damage");
        Session.Characters["time_weaver"].SetTacticalPosition(new(20,10));_fixtures.Single(f=>f.CharacterId=="time_weaver").ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("time_weaver"));ToggleTransfer();Check(_transferMode&&_transferPlans.Count>0,"Transfer preview setup");
        Step(duration);Check(!_transferMode&&_transferPlans.Count==0&&Session.Characters["time_weaver"].TacticalPosition==new PawnsOfDivinity.Core.Abilities.GridPosition(20,10),"Timeout cancels transfer preview and keeps committed position");
        ResetMatch();Session.SetDevelopmentTimerEnabled(true);StageAttackDemonstration();ConfirmBasicAttack();before=Session.ActivationTimer.RemainingSeconds;Step(100);
        Check(Session.ActivationTimer.Pause==ActivationTimerPause.CombatPresentation&&Session.ActivationTimer.RemainingSeconds==before,"Floating combat playback pauses next player's clock");
        _combatHud._Process(100);AdvanceActivationClock();RefreshMatchHud();Check(Session.PendingCombatClockPresentation is null,"Playback acknowledgment releases pause");
        StageObjectiveVictoryDemonstration();Check(Session.ActivationTimer.Stopped&&_timerEnabled.Disabled&&_timerPause.Disabled,"Victory stops clock and DEV controls");Step(1000);
        Check(Session.ActivationTimer.Stopped,"No stale timeout after victory");ResetMatch();Check(!Session.ActivationTimer.Stopped&&Session.TimeoutCount==0&&Session.ActivationTimer.RemainingSeconds==duration,"Reset restores fresh timer/configuration");
        Session=Session.CreateResetSession(timeSource:new MonotonicActivationTimeSource());ResetMatch();
    }
}
