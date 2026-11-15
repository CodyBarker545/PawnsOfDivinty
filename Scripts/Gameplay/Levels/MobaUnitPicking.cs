using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

public readonly record struct MobaModelHit(string FixtureId,double ScreenDistanceSquared);

/// <summary>A visible model hit takes precedence over the tile projected behind its head/body.</summary>
public static class MobaUnitPicking
{
    public static string? Choose(IEnumerable<MobaModelHit> modelHits,string? floorOccupant)=>modelHits
        .OrderBy(h=>h.ScreenDistanceSquared).ThenBy(h=>h.FixtureId,StringComparer.Ordinal)
        .Select(h=>h.FixtureId).FirstOrDefault()??floorOccupant;
}
