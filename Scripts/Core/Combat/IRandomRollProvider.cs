namespace PawnsOfDivinity.Core.Combat;

/// <summary>Supplies a new roll each time a strike needs one.</summary>
public interface IRandomRollProvider
{
    /// <summary>Returns a whole number from 0 to 99, including both ends.</summary>
    int NextRoll();
}
