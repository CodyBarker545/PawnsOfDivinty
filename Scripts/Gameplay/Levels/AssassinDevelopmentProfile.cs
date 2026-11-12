using System;
using PawnsOfDivinity.Core.Combat;
namespace PawnsOfDivinity.Gameplay.Levels;
public sealed record AssassinDevelopmentProfile(bool EnableUnarmedFallback=false,int UnarmedPower=0,int UnarmedAccuracy=90,bool SmokeOverObstacles=true)
{
    public void Validate(){if(UnarmedPower<0||UnarmedAccuracy is <0 or >100)throw new ArgumentOutOfRangeException(nameof(UnarmedPower),"DEV unarmed Power must be nonnegative and accuracy 0–100.");}
    public AttackConfiguration? Unarmed=>EnableUnarmedFallback?AttackConfiguration.CreateUnarmed(UnarmedPower,UnarmedAccuracy):null;
}
