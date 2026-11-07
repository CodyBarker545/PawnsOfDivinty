namespace PawnsOfDivinity.Core.Statuses;

/// <summary>One completed tick, separating calculated damage, shield absorption and actual HP loss.</summary>
public sealed record PoisonTickResult
{
    public int TickNumber { get; }
    public int CalculatedDamage { get; }
    public int ShieldHPAbsorbed { get; }
    public int HealthLost { get; }
    public int RemainingHealth { get; }
    public bool Defeated { get; }

    internal PoisonTickResult(int tickNumber, int calculatedDamage, int shieldHPAbsorbed,
        int healthLost, int remainingHealth, bool defeated)
    {
        TickNumber = tickNumber;
        CalculatedDamage = calculatedDamage;
        ShieldHPAbsorbed = shieldHPAbsorbed;
        HealthLost = healthLost;
        RemainingHealth = remainingHealth;
        Defeated = defeated;
    }
}
