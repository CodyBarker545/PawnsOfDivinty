using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity;

public enum RoundCleanupStatus
{
    Completed, RejectedWrongPhase, RejectedInvalidRound, RejectedActivationsIncomplete,
    AlreadyProcessed, PartiallyApplied, Failed, ManualResolutionRequired
}
public enum RoundCleanupService { TemporaryModifiers, WailingRampart, SpiritFrost, Smoke, Taunts, Sentinel }
public enum CleanupServiceStatus { Completed, Failed, NotStarted }

/// <summary>An actual identity returned by an expiration service, never an estimated expiration.</summary>
public sealed record CleanupEffect(string Id, string SourceCharacterId, string? RecipientCharacterId = null,
    SentinelPreparationStatus? SentinelStatus = null);

public sealed record CleanupServiceResult
{
    public RoundCleanupService Service { get; }
    public string? CharacterId { get; }
    public CleanupServiceStatus Status { get; }
    public IReadOnlyList<CleanupEffect>? Effects { get; }
    public int? ExpiredOrRetiredCount => Effects?.Count;
    public string? FailureType { get; }
    public string? FailureMessage { get; }
    /// <summary>A throwing service may have changed state even though it returned no expiration list.</summary>
    public bool MutationMayHaveOccurred => Status == CleanupServiceStatus.Failed;
    internal CleanupServiceResult(RoundCleanupService service, string? characterId, CleanupServiceStatus status,
        IEnumerable<CleanupEffect>? effects = null, string? failureType = null, string? failureMessage = null)
    {
        Service = service; CharacterId = characterId; Status = status;
        Effects = effects is null ? null : Array.AsReadOnly(effects.ToArray());
        FailureType = failureType; FailureMessage = failureMessage;
    }
}

/// <summary>Immutable evidence of an attempt or rejection. No mutable Character, service, or exception is retained.</summary>
public sealed record RoundCleanupResult
{
    public int RequestedRound { get; }
    public int LifecycleRound { get; }
    public RoundPhase Phase { get; }
    public RoundCleanupStatus Status { get; }
    public string Reason { get; }
    public IReadOnlyList<CleanupServiceResult> Services { get; }
    public RoundCleanupStatus? PreviousAttemptStatus { get; }
    public int? PreviousAttemptRound { get; }
    /// <summary>Zero for rejections and duplicate requests, even when Services retains original attempt evidence.</summary>
    public int ServicesInvokedThisRequest => Status is RoundCleanupStatus.Completed or RoundCleanupStatus.PartiallyApplied or RoundCleanupStatus.Failed
        ? Services.Count(s => s.Status != CleanupServiceStatus.NotStarted) : 0;
    public bool RequiresManualResolution => Status is RoundCleanupStatus.PartiallyApplied or RoundCleanupStatus.Failed or RoundCleanupStatus.ManualResolutionRequired;
    internal RoundCleanupResult(int requestedRound, int lifecycleRound, RoundPhase phase, RoundCleanupStatus status,
        string reason = "", IEnumerable<CleanupServiceResult>? services = null, RoundCleanupStatus? previousAttemptStatus = null,
        int? previousAttemptRound = null)
    {
        RequestedRound = requestedRound; LifecycleRound = lifecycleRound; Phase = phase; Status = status; Reason = reason;
        Services = Array.AsReadOnly(services?.ToArray() ?? Array.Empty<CleanupServiceResult>());
        PreviousAttemptStatus = previousAttemptStatus;
        PreviousAttemptRound = previousAttemptRound;
    }
}
