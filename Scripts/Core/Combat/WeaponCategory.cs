namespace PawnsOfDivinity.Core.Combat;

/// <summary>Weapon groups used in combat, from spec v0.2, section 4.1.</summary>
public enum WeaponCategory
{
    Sword = 0,
    Axe = 1,
    Lance = 2,
    Bow = 3,
    MagicScroll = 4,
    // An attack without equipped-weapon benefits, not a shop weapon category.
    Unarmed = 5
}
