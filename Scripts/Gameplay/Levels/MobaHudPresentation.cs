using System;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Read-only HUD data. No action eligibility, combat, equipment or timer rules live here.</summary>
public sealed record MobaHudPresentation(string Name,string Team,string Health,float HealthFraction,
    string Readiness,string Stats,string Effects,string Inventory)
{
    public static MobaHudPresentation Capture(MobaHotseatSession session)
    {
        if(session.SelectedCharacter is not { } c)return new("No unit selected","Select a friendly unit","—",0,
            "Awaiting selection","STR —    DEF —    INT —    RES —\nWIS —    SPD —    MOV —","No active unit","Equipment / items\nSelect a unit to inspect.\nInventory management unavailable.");
        var s=session.Capture(c.Id);var b=c.BaseStats;
        var cooldowns=c.Cooldowns.Cooldowns.OrderBy(e=>e.Key,StringComparer.Ordinal).Select(e=>$"{c.Definition!.Abilities.First(a=>a.Id==e.Key).DisplayName}: {(e.Value.ReadyRound>session.Round?$"R{e.Value.ReadyRound}":"ready")}");
        var effects=s.Statuses.Concat(cooldowns).ToArray();
        return new(c.Name,c.TeamId==1?"BLUE TEAM":"RED TEAM",$"{s.HP} / {s.MaxHP} HP · SHD {s.ShieldHP}",
            (float)s.HP/s.MaxHP,s.Defeated?"DEFEATED":s.HasActed?"ACTIVATED":session.Activations.ActiveCharacter==c?"ACTIVE":"READY",
            $"STR {b.Strength}    DEF {c.EffectiveDefense}    INT {b.Intelligence}    RES {c.EffectiveResistance}\nWIS {b.Wisdom}    SPD {b.Speed}    MOV {s.Movement}  ({session.RemainingMovement} available)",
            effects.Length==0?"No active statuses or cooldowns":string.Join(" · ",effects),
            $"Scroll: {c.EquippedScroll?.Name??"none equipped"}\nDEV combat loadout in use.\nItem management unavailable.");
    }
}

/// <summary>Bounded HUD reservation in viewport coordinates, independent of tactical coordinates.</summary>
public readonly record struct MobaHudMetrics(double Width,double Height,double Top,double Bottom)
{
    public double BattlefieldHeight=>Height-Top-Bottom;
    public static MobaHudMetrics For(double width,double height)
    {
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<1280||height<720)throw new ArgumentOutOfRangeException(nameof(width),"Tactical HUD requires at least 1280×720 logical pixels.");
        return new(width,height,54,Math.Clamp(height*.30,228,280));
    }
    public bool ContainsBattlefield(double x,double y)=>x>=0&&x<Width&&y>=Top&&y<Height-Bottom;
}
