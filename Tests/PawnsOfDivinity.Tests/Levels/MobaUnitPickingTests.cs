using PawnsOfDivinity.Gameplay.Levels;
using Xunit;

namespace PawnsOfDivinity.Tests.Levels;
public class MobaUnitPickingTests
{
    [Fact] public void ModelHitWinsOverGroundOccupantBehindItsUpperBody()=>Assert.Equal("enemy",MobaUnitPicking.Choose(new[]{new MobaModelHit("enemy",900)},"ally"));
    [Fact] public void ModelCanBeChosenWithNoOccupiedFloorUnderTheCursor()=>Assert.Equal("enemy",MobaUnitPicking.Choose(new[]{new MobaModelHit("enemy",50)},null));
    [Fact] public void OverlappingModelsUseClosestProjectedModelCenter()=>Assert.Equal("closer",MobaUnitPicking.Choose(new[]{new MobaModelHit("farther",90),new MobaModelHit("closer",10)},"ground"));
    [Fact] public void EqualModelDistancesAreDeterministic()=>Assert.Equal("a",MobaUnitPicking.Choose(new[]{new MobaModelHit("z",10),new MobaModelHit("a",10)},null));
    [Fact] public void GroundRemainsAFallbackWhenNoVisibleModelIsHit()=>Assert.Equal("ground",MobaUnitPicking.Choose(Array.Empty<MobaModelHit>(),"ground"));
    [Fact] public void EmptyGroundAndNoModelClearTheTarget()=>Assert.Null(MobaUnitPicking.Choose(Array.Empty<MobaModelHit>(),null));
}
