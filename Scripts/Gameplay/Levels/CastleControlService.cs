using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;
using PawnsOfDivinity.Core.Characters;

namespace PawnsOfDivinity.Gameplay.Levels;
public sealed record MobaZoneControl(string Id,int? OwnerTeamId,bool Contested,int? OccupyingTeamId,bool IsHome=false);
public sealed record MobaControlChange(string ZoneId,int? PreviousOwner,int? Owner,bool Contested,bool CastleCapture,string Text);

/// <summary>Authoritative beta ownership, independent of Godot. Occupancy is always read from living Characters.</summary>
public sealed class CastleControlService
{
    private readonly MobaBetaLayout _layout;
    private readonly IReadOnlyDictionary<string,Character> _characters;
    private readonly Dictionary<string,int?> _owners;
    private readonly HashSet<string> _processed=new(StringComparer.Ordinal);
    private readonly Dictionary<string,MobaZoneControl> _previous=new(StringComparer.Ordinal);
    public string ObjectiveId {get;}
    public CastleControlService(MobaBetaLayout layout,IReadOnlyDictionary<string,Character> characters)
    {
        _layout=layout;_characters=characters;
        _owners=layout.Castles.ToDictionary(c=>c.Id,c=>c.InitialTeamId==0?(int?)null:c.InitialTeamId,StringComparer.Ordinal);
        ObjectiveId=layout.Source.Elements.Single(e=>e.Kind=="Objective").Id;
        foreach(var zone in AllZones())_previous.Add(zone.Id,zone);
    }
    private (bool Contested,int? Occupier) Occupancy(IReadOnlySet<GridPosition> cells)
    {
        var teams=_characters.Values.Where(c=>!c.IsDefeated&&c.TacticalPosition is { } p&&cells.Contains(p)).Select(c=>c.TeamId).Distinct().ToArray();
        return(teams.Length>1,teams.Length==1?teams[0]:null);
    }
    public MobaZoneControl Castle(string id)
    {
        var castle=_layout.Castles.SingleOrDefault(c=>c.Id==id)??throw new ArgumentException($"Unknown castle '{id}'.");
        var o=Occupancy(castle.CaptureCells);return new(id,_owners[id],o.Contested,o.Occupier,castle.IsHome);
    }
    // DEVELOPMENT objective control: live uncontested occupation; vacated or contested center has no controller.
    public MobaZoneControl Objective
    {get{var o=Occupancy(_layout.Objective);return new(ObjectiveId,o.Occupier,o.Contested,o.Occupier);}}
    public IReadOnlyList<MobaZoneControl> AllZones()=>_layout.Castles.Select(c=>Castle(c.Id)).Append(Objective).ToArray();
    internal IReadOnlyList<MobaControlChange> Reconcile(string eventId,bool allowCastleCapture=true)
    {
        if(!_processed.Add(eventId))return Array.Empty<MobaControlChange>();
        var changes=new List<MobaControlChange>();
        foreach(var castle in _layout.Castles.Where(c=>allowCastleCapture&&!c.IsHome))
        {
            var zone=Castle(castle.Id);
            if(!zone.Contested&&zone.OccupyingTeamId is { } team&&zone.OwnerTeamId!=team)
            {
                _owners[zone.Id]=team;
                changes.Add(new(zone.Id,zone.OwnerTeamId,team,false,true,$"Player {team} captured {zone.Id}."));
            }
        }
        foreach(var zone in AllZones())
        {
            var old=_previous[zone.Id];
            if(old.Contested!=zone.Contested)changes.Add(new(zone.Id,old.OwnerTeamId,zone.OwnerTeamId,zone.Contested,false,$"{zone.Id}: {(zone.Contested?"CONTESTED — transfers blocked":"uncontested")} ."));
            if(zone.Id==ObjectiveId&&old.OwnerTeamId!=zone.OwnerTeamId)
                changes.Add(new(zone.Id,old.OwnerTeamId,zone.OwnerTeamId,zone.Contested,false,$"{zone.Id}: {(zone.OwnerTeamId is { } p?$"Player {p} controls":"neutral / no sole controller")} (DEV occupation)."));
            _previous[zone.Id]=zone;
        }
        return changes.AsReadOnly();
    }
}
