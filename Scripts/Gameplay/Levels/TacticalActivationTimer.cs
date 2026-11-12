using System;
using System.Diagnostics;

namespace PawnsOfDivinity.Gameplay.Levels;
public interface IActivationTimeSource { double NowSeconds {get;} }
public sealed class MonotonicActivationTimeSource : IActivationTimeSource
{private readonly Stopwatch _watch=Stopwatch.StartNew();public double NowSeconds=>_watch.Elapsed.TotalSeconds;}
public sealed record ActivationTimerConfiguration(bool Enabled=true,double DurationSeconds=30)
{
    public void Validate()
    {if(!double.IsFinite(DurationSeconds)||DurationSeconds<=0)throw new ArgumentOutOfRangeException(nameof(DurationSeconds),"Development duration must be finite and greater than zero.");}
}
public enum ActivationTimerPause { None, Disabled, Development, Command, MovementPresentation, CombatPresentation, Victory, Recovery, NoEligiblePlayer }
public sealed record CombatClockPresentation(Guid MatchId,long Sequence);

/// <summary>Only countdown state. Session commands decide expiry outcomes; this class never chooses/ends a unit.</summary>
public sealed class TacticalActivationTimer
{
    private readonly IActivationTimeSource _source;
    private double _last=double.NegativeInfinity;
    public Guid MatchId {get;}=Guid.NewGuid();
    public ActivationTimerConfiguration Configuration {get;}
    public bool Enabled {get;internal set;}
    public bool DevelopmentPaused {get;internal set;}
    public double RemainingSeconds {get;private set;}
    public long TurnSerial {get;private set;}=-1;
    public int Round {get;private set;}
    public int? PlayerId {get;private set;}
    public bool Stopped {get;private set;}
    public ActivationTimerPause Pause {get;private set;}=ActivationTimerPause.NoEligiblePlayer;
    public bool IsUrgent=>Enabled&&!Stopped&&RemainingSeconds<=Math.Min(5,Configuration.DurationSeconds*.25);
    public bool Expired=>Enabled&&!Stopped&&Pause==ActivationTimerPause.None&&RemainingSeconds<=0;
    public TacticalActivationTimer(ActivationTimerConfiguration config,IActivationTimeSource source)
    {config.Validate();Configuration=config;Enabled=config.Enabled;_source=source;RemainingSeconds=config.DurationSeconds;_last=ReadTime();}
    private double ReadTime()
    {double now=_source.NowSeconds;if(!double.IsFinite(now)||now<_last)throw new InvalidOperationException("Activation time source must be finite and monotonic.");return now;}
    internal void Pulse(long serial,int round,int? player,ActivationTimerPause pause,bool victory)
    {
        if(victory||Stopped){Stopped=true;PlayerId=null;RemainingSeconds=0;Pause=ActivationTimerPause.Victory;return;}
        double now=ReadTime();double elapsed=now-_last;_last=now;
        if(TurnSerial!=serial||PlayerId!=player||Round!=round)
        {TurnSerial=serial;Round=round;PlayerId=player;RemainingSeconds=Configuration.DurationSeconds;elapsed=0;}
        var next=player is null?ActivationTimerPause.NoEligiblePlayer:!Enabled?ActivationTimerPause.Disabled:DevelopmentPaused?ActivationTimerPause.Development:pause;
        if(next==ActivationTimerPause.None&&Pause==ActivationTimerPause.None)RemainingSeconds=Math.Max(0,RemainingSeconds-elapsed);
        Pause=next;
    }
}
