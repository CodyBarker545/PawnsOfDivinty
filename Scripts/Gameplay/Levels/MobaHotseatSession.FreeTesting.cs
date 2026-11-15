using System.Linq;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Core.Turns;

namespace PawnsOfDivinity.Gameplay.Levels;

public sealed partial class MobaHotseatSession
{
    /// <summary>Explicit development wiring, not competitive rules. Normal sessions default false.</summary>
    public bool FreeTesting {get;}
    private void PrepareFreeTestActor(Character actor)
    {
        actor.ResetActivation();
        var round=Round;
        Lifecycle=new RoundLifecycleManager(round);
        // Each explicit test selection owns one production action/movement context. Round effects
        // remain paused: acknowledge phases without ticking statuses, cooldowns or scores.
        while(Lifecycle.CurrentPhase!=RoundPhase.AlternatingActivations)Lifecycle.AdvancePhase();
        // Commands still resolve through production combat/ability executors. Give the chosen
        // side a fresh action context instead of advancing the disconnected match turn loop.
        Activations=new ActivationManager(Characters.Values.Where(c=>c.TeamId==1),Characters.Values.Where(c=>c.TeamId==2),actor.TeamId,Lifecycle);
        _movement=_movementFactory(Activations);
        _activationSerial++;CancelAssassinTargeting();_approaches.Clear();
    }
}
