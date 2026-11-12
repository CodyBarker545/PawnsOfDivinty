using System;
using System.Linq;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed partial class MobaHotseatSession
{
    public TacticalActivationTimer ActivationTimer {get;private set;}=null!;
    private IActivationTimeSource _timeSource=null!;
    private long _activationSerial=1,_combatClockSequence;
    private int _commandDepth;
    private bool _processingTimeout;
    public long TimeoutCount {get;private set;}
    public string LastTimeoutMessage {get;private set;}="";
    public CombatClockPresentation? PendingCombatClockPresentation {get;private set;}
    private void InitializeTimer(ActivationTimerConfiguration config,IActivationTimeSource? source)
    {_timeSource=source??new MonotonicActivationTimeSource();ActivationTimer=new(config,_timeSource);PulseClock();}
    private ActivationTimerPause ClockPause=>_commandDepth>0||_processingTimeout?ActivationTimerPause.Command:RequiresManualRecovery?ActivationTimerPause.Recovery:
        PendingVisualMovement is not null?ActivationTimerPause.MovementPresentation:PendingCombatClockPresentation is not null?ActivationTimerPause.CombatPresentation:ActivationTimerPause.None;
    private void PulseClock()=>ActivationTimer?.Pulse(_activationSerial,Round,
        Lifecycle.CurrentPhase==RoundPhase.AlternatingActivations?CurrentPlayer:null,ClockPause,Victory.IsFinished);
    /// <summary>Synchronous polling: at most one timeout per pulse; no frame/callback can spend a later turn.</summary>
    public bool UpdateActivationTimer()
    {
        PulseClock();
        if(_processingTimeout||_commandDepth>0||!ActivationTimer.Expired)return false;
        _processingTimeout=true;
        try
        {
            var actor=Activations.ActiveCharacter??(SelectedCharacter is { } selected&&CanSelect(selected.Id)?selected:null)
                ??Characters.Values.Where(c=>CanSelect(c.Id)).OrderBy(c=>c.Id,StringComparer.Ordinal).FirstOrDefault();
            if(actor is null)return false;
            if(Activations.ActiveCharacter is null)Activations.StartActivation(actor);
            Activations.ForfeitActivationForTimeout();SelectedFixtureId=null;TimeoutCount++;
            var next=AdvanceCompletedRound();
            LastTimeoutMessage=$"TIMEOUT: Player {actor.TeamId} forfeited {actor.Name} [{actor.Id}]; committed effects retained."+next.Reason;
            return true;
        }
        catch(InvalidOperationException error)
        {RequiresManualRecovery=true;LastTimeoutMessage=$"Timer could not complete the production activation: {error.Message} Reset/recovery required.";return false;}
        finally{_processingTimeout=false;PulseClock();}
    }
    private sealed class TimedCommand : IDisposable
    {
        private readonly MobaHotseatSession _session;
        public bool Accepted {get;}
        public TimedCommand(MobaHotseatSession session)
        {
            _session=session;
            if(session._commandDepth==0&&!session._processingTimeout&&session.UpdateActivationTimer())return;
            Accepted=true;session._commandDepth++;session.PulseClock();
        }
        public void Dispose(){if(!Accepted)return;_session.PulseClock();_session._commandDepth--;_session.PulseClock();}
    }
    private TimedCommand BeginTimedCommand()=>new(this);
    public BattlefieldCommandResult SetDevelopmentTimerPaused(bool paused)
    {
        if(Victory.IsFinished||_commandDepth>0||_processingTimeout)return new(BattlefieldOutcome.Rejected,"Timer controls are unavailable during a command or after victory.");
        UpdateActivationTimer();ActivationTimer.DevelopmentPaused=paused;PulseClock();
        return new(BattlefieldOutcome.Completed,paused?"DEV activation clock paused.":"DEV activation clock resumed.");
    }
    public BattlefieldCommandResult SetDevelopmentTimerEnabled(bool enabled)
    {
        if(Victory.IsFinished||_commandDepth>0||_processingTimeout)return new(BattlefieldOutcome.Rejected,"Timer controls are unavailable during a command or after victory.");
        UpdateActivationTimer();ActivationTimer.Enabled=enabled;PulseClock();
        return new(BattlefieldOutcome.Completed,enabled?"DEV timer enabled; same activation allowance resumes.":"DEV untimed hotseat mode enabled.");
    }
    public CombatClockPresentation? HoldClockForCombatPresentation(string requestId)
    {
        if(Victory.IsFinished||!CombatHistory.Records.Any(r=>r.RequestId==requestId))return null;
        UpdateActivationTimer();PendingCombatClockPresentation=new(ActivationTimer.MatchId,++_combatClockSequence);PulseClock();return PendingCombatClockPresentation;
    }
    public bool AcknowledgeCombatClockPresentation(CombatClockPresentation token)
    {
        if(token!=PendingCombatClockPresentation||token.MatchId!=ActivationTimer.MatchId)return false;
        PulseClock();PendingCombatClockPresentation=null;PulseClock();return true;
    }
}
