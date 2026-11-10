namespace PawnsOfDivinity.Core.Combat;

/// <summary>A stored attack's outcome and actual shield/HP losses at resolution.</summary>
public sealed record DelayedAttackResolution
{
    public DelayedAttack Attack { get; }
    public int ShieldHPAbsorbed { get; }
    public int HealthLost { get; }
    public int RemainingHealth { get; }
    public bool RecipientDefeated { get; }
    public bool SkippedDefeatedTarget { get; }

    internal DelayedAttackResolution(DelayedAttack attack, int shieldHPAbsorbed, int healthLost,
        int remainingHealth, bool recipientDefeated, bool skippedDefeatedTarget)
    {
        Attack = attack;
        ShieldHPAbsorbed = shieldHPAbsorbed;
        HealthLost = healthLost;
        RemainingHealth = remainingHealth;
        RecipientDefeated = recipientDefeated;
        SkippedDefeatedTarget = skippedDefeatedTarget;
    }
}
