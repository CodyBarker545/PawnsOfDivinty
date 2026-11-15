using Godot;
using System;
using System.Linq;
using PawnsOfDivinity.Gameplay.Levels;

public partial class MobaBetaBattlefield
{
    private bool _captureFreeTesting;
    private async void FrameFreeTestLaunch()
    {
        // Fit after the timer/readiness controls have been removed and the viewport has its final size.
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        if(Session.FreeTesting)ResetCamera();
    }
    private void VerifyFreeTestingSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Free testing: "+why);}
        Check(FreeTesting,"authored beta scene defaults to free testing");
        Session=new(Layout,StartingPlayer,scoringProfile:DevelopmentScoring.Snapshot(),timerConfiguration:DevelopmentTimer.Snapshot(),assassinProfile:DevelopmentAssassin.Snapshot(),freeTesting:true);
        ResetMatch();RunCommand(()=>Session.Select("nigel"));
        Check(Session.SelectedFixtureId=="nigel"&&!_timerLabel.Visible&&!_endActivation.Visible&&!Session.ActivationTimer.Enabled,"Red selection at launch, no timer/turn controls");
        var origin=Session.Characters["nigel"].TacticalPosition;
        MoveAt(new(14,10));Check(Session.Characters["nigel"].TacticalPosition==origin&&Session.RemainingMovement==4,"Nigel cannot exceed MOV4");
        MoveAt(new(25,12));Check(Session.PendingVisualMovement is not null,"normal movement animation retained");AdvanceMovementViews(20);RefreshMatchHud();
        Check(Session.RemainingMovement==0&&Session.CanAttack,"one unsplit movement spent; Main Action remains");
        MoveAt(new(24,12));Check(Session.Characters["nigel"].TacticalPosition==new PawnsOfDivinity.Core.Abilities.GridPosition(25,12),"second movement rejected");
        RunCommand(()=>Session.Select("hooded_assassin"));Check(Session.SelectedFixtureId=="hooded_assassin","switch directly to Blue");
        Session.Characters["hooded_assassin"].SetTacticalPosition(new(14,9));Session.Characters["nigel"].SetTacticalPosition(new(15,10));
        foreach(string id in new[]{"hooded_assassin","nigel"})_fixtures.Single(f=>f.CharacterId==id).ReconcileToRuntime(Layout.Board);
        MoveAt(new(14,10));AdvanceMovementViews(20);RefreshMatchHud();Check(Session.Characters["hooded_assassin"].TacticalPosition==new PawnsOfDivinity.Core.Abilities.GridPosition(14,10),"Blue uses normal movement after Red");
        _attack.EmitSignal(Button.SignalName.Pressed);_attackTargetId="nigel";RefreshMatchHud();_confirmAttack.EmitSignal(Button.SignalName.Pressed);
        Check(Session.CombatHistory.Records.Count==1&&Session.Round==1,"production combat retained, round/turn loop disconnected");
        _combatHud._Process(100);AdvanceActivationClock();RunCommand(()=>Session.Select("hooded_assassin"));
        Check(Session.SelectedFixtureId=="hooded_assassin"&&Session.CanAttack,"same unit can act again");
        ResetMatch();Check(Session.FreeTesting&&!Session.ActivationTimer.Enabled,"reset preserves free testing");
        Session=new(Layout,StartingPlayer,scoringProfile:DevelopmentScoring.Snapshot(),timerConfiguration:DevelopmentTimer.Snapshot(),assassinProfile:DevelopmentAssassin.Snapshot());ResetMatch();
        GD.Print("FREE TESTING VERIFIED: either-team selection, normal MOV/path/one-movement restrictions and animation, production combat, no timer/turn/round progression, reset and retained normal mode.");
    }
}
