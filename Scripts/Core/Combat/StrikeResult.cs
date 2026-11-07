namespace PawnsOfDivinity.Core.Combat;

/// <summary>Records one strike before its damage is applied to health.</summary>
public sealed record StrikeResult
{
    public bool Hit { get; }
    public int HitChance { get; }
    public int Damage { get; }
    public WeaponTriangleOutcome TriangleOutcome { get; }

    // Results are created by combat code after the strike has been checked and rolled.
    internal StrikeResult(bool hit, int hitChance, int damage, WeaponTriangleOutcome triangleOutcome)
    {
        Hit = hit;
        HitChance = hitChance;
        Damage = hit ? damage : 0;
        TriangleOutcome = triangleOutcome;
    }
}
