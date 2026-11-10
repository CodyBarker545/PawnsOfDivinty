namespace PawnsOfDivinity.Core.Abilities;

/// <summary>Describes how an ability is initiated; it does not execute or schedule anything.</summary>
public enum AbilityClassification
{
    Active = 0,
    Passive = 1,
    Reaction = 2
}
