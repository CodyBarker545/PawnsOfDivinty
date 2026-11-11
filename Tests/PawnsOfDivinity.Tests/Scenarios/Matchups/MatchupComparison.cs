using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PawnsOfDivinity.Tests.Scenarios.Matchups;

public sealed record MatchupDifference(string Path, string Baseline, string Variant);
public sealed record MatchupComparisonResult
{
    public bool IsComparable { get; }
    public string Reason { get; }
    public string Rationale { get; }
    public IReadOnlyList<MatchupDifference> Differences { get; }
    public long? HPDamageDifference { get; }
    public long? ObservedMatchedDefenseHPReduction { get; }
    internal MatchupComparisonResult(bool comparable, string reason, string rationale, IEnumerable<MatchupDifference> differences,
        long? damageDifference = null, long? defenseReduction = null)
    {
        IsComparable = comparable; Reason = reason; Rationale = rationale;
        Differences = Array.AsReadOnly(differences.ToArray()); HPDamageDifference = damageDifference;
        ObservedMatchedDefenseHPReduction = defenseReduction;
    }
}

/// <summary>Requires reviewed input differences. A comparable fixture is not automatically fair or competitively balanced.</summary>
public static class MatchupComparison
{
    public static IReadOnlyList<MatchupDifference> DescribeDifferences(MatchupDefinition baseline, MatchupDefinition variant)
    {
        ArgumentNullException.ThrowIfNull(baseline); ArgumentNullException.ThrowIfNull(variant);
        var differences = new List<MatchupDifference>();
        void Walk(JsonElement a, JsonElement b, string path)
        {
            if (a.ValueKind == JsonValueKind.Object && b.ValueKind == JsonValueKind.Object)
            {
                foreach (var key in a.EnumerateObject().Select(p => p.Name).Union(b.EnumerateObject().Select(p => p.Name)).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (path == "$" && key == "Id") continue; // Variant identity is not a gameplay input.
                    Walk(a.TryGetProperty(key, out var av) ? av : default, b.TryGetProperty(key, out var bv) ? bv : default, $"{path}.{key}");
                }
            }
            else if (a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array && a.GetArrayLength() == b.GetArrayLength())
                for (int i = 0; i < a.GetArrayLength(); i++) Walk(a[i], b[i], $"{path}[{i}]");
            else
            {
                string Text(JsonElement value) => value.ValueKind == JsonValueKind.Undefined ? "unavailable" : value.GetRawText();
                if (Text(a) != Text(b)) differences.Add(new(path, Text(a), Text(b)));
            }
        }
        Walk(JsonSerializer.SerializeToElement(baseline), JsonSerializer.SerializeToElement(variant), "$");
        return Array.AsReadOnly(differences.ToArray());
    }

    public static MatchupComparisonResult Compare(MatchupResult baseline, MatchupResult variant,
        IEnumerable<string> declaredDifferencePaths, string rationale, string? defensiveRecipientId = null)
    {
        ArgumentNullException.ThrowIfNull(baseline); ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(declaredDifferencePaths); ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        var changes = DescribeDifferences(baseline.Definition, variant.Definition);
        var declared = declaredDifferencePaths.ToHashSet(StringComparer.Ordinal);
        if (baseline.Definition.Mode != variant.Definition.Mode)
            return new(false, "Different execution modes cannot be combined", rationale, changes);
        if (baseline.Status != MatchupStatus.Completed || variant.Status != MatchupStatus.Completed)
            return new(false, "Both experiments must complete before numerical comparison", rationale, changes);
        if (changes.Any(d => !declared.Contains(d.Path)) || declared.Any(p => !changes.Any(d => d.Path == p)))
            return new(false, "Declared differences do not exactly match the recorded input changes", rationale, changes);
        long? defense = null;
        if (defensiveRecipientId is not null)
        {
            var targetIndex = baseline.Definition.Participants.ToList().FindIndex(p => p.InstanceId == defensiveRecipientId);
            bool DefenseChange(MatchupDifference difference)
            {
                if (targetIndex >= 0 && difference.Path == $"$.Participants[{targetIndex}].Shield") return true;
                for (int i = 0; i < baseline.Definition.Participants.Count; i++)
                    if (baseline.Definition.Participants[i].DefinitionId == "gravebound_colossus" &&
                        (difference.Path == $"$.Participants[{i}].Position.X" || difference.Path == $"$.Participants[{i}].Position.Y")) return true;
                return false;
            }
            if (targetIndex >= 0 && changes.Count > 0 && changes.All(DefenseChange) &&
                baseline.Survivability.Any(s => s.InstanceId == defensiveRecipientId) && variant.Survivability.Any(s => s.InstanceId == defensiveRecipientId))
                defense = baseline.Survivability.Single(s => s.InstanceId == defensiveRecipientId).ObservedHPLoss
                    - variant.Survivability.Single(s => s.InstanceId == defensiveRecipientId).ObservedHPLoss;
        }
        return new(true, "Documented controlled input comparison; no balance conclusion", rationale, changes,
            variant.Metrics!.ObservedHPDamage - baseline.Metrics!.ObservedHPDamage, defense);
    }
}
