using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters.Definitions;
using PawnsOfDivinity.Core.Turns.Restrictions;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

public enum MobaCommandMode { None, Move, Attack, Abilities, Transfer }
public readonly record struct MobaCommandVisual(bool Enabled,bool Active);

/// <summary>Read-only projection of existing command modes and session eligibility, never a command store.</summary>
public static class MobaCommandPresentation
{
    public static MobaCommandMode Mode(bool selected,bool attack,bool abilities,bool transfer,bool movementAvailable)=>
        !selected?MobaCommandMode.None:abilities?MobaCommandMode.Abilities:transfer?MobaCommandMode.Transfer:attack?MobaCommandMode.Attack:movementAvailable?MobaCommandMode.Move:MobaCommandMode.None;
    public static MobaCommandVisual State(bool enabled,MobaCommandMode mode,MobaCommandMode command)=>new(enabled,enabled&&mode==command);
    public static bool CanEndActivation(MobaHotseatSession session)
    {
        if(session.FreeTesting)return false;
        if(session.SelectedCharacter is not { } c||!session.CanSelect(c.Id))return false;
        var restriction=session.Activations.ActionRestrictions.GetActive(c.Id);
        return restriction is null||session.Activations.ActionRestrictions.SourceDefinitelyInvalid(restriction);
    }

    // The panel remains inspectable on cooldown. Only its Preview/execute entry is gated.
    public static bool CanPreviewAssassinAbility(MobaHotseatSession session,string abilityId)
    {
        var c=session.SelectedCharacter;
        if(!session.CanAttack||c?.Definition?.Id!=HoodedAssassin.Definition.Id||abilityId is not (HoodedAssassin.SmokeBombId or HoodedAssassin.ShadowStrikeId))return false;
        if(session.Activations.ActiveCharacter is not null)return AbilityEligibility.Evaluate(c,abilityId,session.Activations).IsAllowed;
        var restriction=session.Activations.ActionRestrictions.GetActive(c.Id);
        if(restriction is not null)
        {
            session.Characters.TryGetValue(restriction.SourceCharacterId,out var source);
            if(!TauntActionPolicy.Evaluate(c,restriction,source,session.Round,false,null).IsAllowed)return false;
        }
        var ability=c.Definition.Abilities.Single(a=>a.Id==abilityId);
        return c.Cooldowns.IsReady(abilityId,session.Round)&&SilenceEligibility.CanUseAbility(c,ability.IsSpellTagged??true);
    }
}
