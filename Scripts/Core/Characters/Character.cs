using System;
using PawnsOfDivinity.Core.Statuses;

namespace PawnsOfDivinity.Core.Characters;

/// <summary>One playable instance with immutable identity/base stats and independent battle state.</summary>
public class Character
{
    //Identity 
    public string Id {get; }
    public string Name {get; }
    public int TeamId {get; }

    // Starting stats for a character.
    public CharacterStats BaseStats {get; }
    public StatusTracker Statuses { get; } = new();

    /// <summary>Defense used in combat. Burn removes 3, with a prototype floor of zero.</summary>
    public int EffectiveDefense => Math.Max(0, BaseStats.Defense - (Statuses.HasStatus(StatusType.Burn) ? 3 : 0));

    /// <summary>Movement allowance after Frostbite's -2 MOV penalty, with a prototype floor of zero.</summary>
    public int EffectiveMovement => Math.Max(0, BaseStats.Movement - (Statuses.HasStatus(StatusType.Frostbite) ? 2 : 0));

    private int? _paralysisSpeedReduction;

    /// <summary>Speed after the configured Paralysis penalty, never below zero.</summary>
    /// <remarks>An active Paralysis requires explicit configuration; spec v0.2 leaves its magnitude undecided.</remarks>
    public int EffectiveSpeed
    {
        get
        {
            if (!Statuses.HasStatus(StatusType.Paralysis)) return BaseStats.Speed;
            int reduction = _paralysisSpeedReduction
                ?? throw new InvalidOperationException("Configure the Paralysis Speed reduction before using effective Speed while Paralysis is active.");
            return Math.Max(0, BaseStats.Speed - reduction);
        }
    }

    /// <summary>Sets this character's prototype Paralysis penalty without applying or refreshing the status.</summary>
    /// <remarks>Zero is an explicit configuration. The setting persists after expiration for later applications.</remarks>
    public void ConfigureParalysisSpeedReduction(int reduction)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reduction);
        _paralysisSpeedReduction = reduction;
    }

    // Changing battle state
    public int CurrentHealth {get; private set; }
    public int ShieldHP { get; private set; }
    private Character? _shieldSource;
    // Ownership belongs to the source instance, even when different managers are used.
    internal Character? SanctuaryShieldRecipient { get; set; }
    public bool HasActedThisRound {get; private set; }
    private int _healsReceivedThisRound;
    public int HealsReceivedThisRound => _healsReceivedThisRound;
    public bool IsDefeated => CurrentHealth == 0; 

    public bool CanAct => !HasActedThisRound && !IsDefeated;

    public Character(
        string id,
        string name,
        int teamId,
        CharacterStats baseStats)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An ID is required.", nameof(id));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A name is required.", nameof(name));

        ArgumentNullException.ThrowIfNull(baseStats);

        // CharacterStats validates its values when constructed.

        Id = id;
        Name = name;
        TeamId = teamId;
        BaseStats = baseStats;
        CurrentHealth = baseStats.MaxHealth;
    }

    /// <summary>Replaces unowned shield HP. Source-owned shields must be changed through their manager.</summary>
    public void SetShieldHP(int shieldHP)
        => SetShieldHP(shieldHP, source: null);

    internal void ValidateShieldAssignment(int shieldHP, Character? source)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shieldHP);
        if (IsDefeated && shieldHP > 0)
            throw new InvalidOperationException("A defeated character cannot receive shield HP.");
        if (ShieldHP > 0 && !ReferenceEquals(_shieldSource, source))
            throw new InvalidOperationException("Changing a shield from another source is not supported.");
    }

    internal void SetShieldHP(int shieldHP, Character? source)
    {
        ValidateShieldAssignment(shieldHP, source);
        if (shieldHP == 0) ReleaseShieldSource();
        else _shieldSource = source;
        ShieldHP = shieldHP;
    }

    internal bool HasShieldFrom(Character source) => ShieldHP > 0 && ReferenceEquals(_shieldSource, source);

    private void ReleaseShieldSource()
    {
        if (_shieldSource is not null && ReferenceEquals(_shieldSource.SanctuaryShieldRecipient, this))
            _shieldSource.SanctuaryShieldRecipient = null;
        _shieldSource = null;
    }

    /// <summary>Uses shield HP first, then health, and returns only the actual health lost.</summary>
    public int TakeDamage(int damage)
    {
        if (damage < 0)
            throw new ArgumentOutOfRangeException(nameof(damage), "Damage cannot be negative.");
        int absorbed = Math.Min(damage, ShieldHP);
        ShieldHP -= absorbed;
        if (ShieldHP == 0) ReleaseShieldSource();

        // Subtract the absorbed portion instead of adding shield and health together.
        // Each subtraction stays within range, even when all values are int.MaxValue.
        int damageTaken = Math.Min(damage - absorbed, CurrentHealth);
        CurrentHealth -= damageTaken;
        if (IsDefeated) Statuses.ClearPoison();

        return damageTaken;
    }

    /// <summary>
    /// Restores health to a living character.
    /// Healing cannot exceed maximum health.
    /// Defeated characters cannot be revived through normal healing.
    /// This method only restores HP; HealingApplicator handles diminishing returns and history.
    /// Ordinary healing does not replenish shield HP.
    /// </summary>
    /// <param name="amount">Amount of healing requested.</param>
    /// <returns>Actual amount of health restored.</returns>
    public int Heal(int amount)
    {
        // Reject invalid healing values.
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Healing cannot be negative."
            );
        }

        // Defeated characters cannot receive normal healing.
        if (IsDefeated)
        {
            return 0;
        }

        // Calculate the actual amount of healing.
        int healthRestored = Math.Min(
            amount,
            BaseStats.MaxHealth - CurrentHealth
        );

        // Restore health.
        CurrentHealth += healthRestored;

        // Return actual health restored.
        return healthRestored;
    }

    /// <summary>Clears received-healing history for a new round without changing health or activation.</summary>
    public void ResetHealingHistory()
    {
        _healsReceivedThisRound = 0;
    }

    // The applicator calls this after healing succeeds. Provisionally, every valid event on
    // a living target counts, including full-health and zero-effective heals. Defeated targets do not.
    internal void RecordHealingReceived()
    {
        _healsReceivedThisRound = checked(_healsReceivedThisRound + 1);
    }

    /// <summary>Marks this instance as having completed its activation.</summary>
    public void EndActivation()
    {
        HasActedThisRound = true;
    }
    /// <summary>Clears the activation flag for a new round; does not heal or revive.</summary>
    public void ResetActivation()
    {
        HasActedThisRound = false;
    }
}

