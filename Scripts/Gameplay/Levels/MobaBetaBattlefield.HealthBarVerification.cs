using Godot;
using System;
using System.Linq;

public partial class MobaBetaBattlefield
{
    private void VerifyHealthBarSmoke()
    {
        void Only(params string[] ids)
        {
            foreach(var fixture in _fixtures)
                if(_combatHud.HealthVisible(fixture.CharacterId)!=ids.Contains(fixture.CharacterId))
                    throw new InvalidOperationException("Health-bar visibility mismatch: "+fixture.CharacterId+"; expected only "+string.Join(",",ids));
        }
        ResetMatch();Only();
        RunCommand(()=>Session.Select("hooded_assassin"));Only("hooded_assassin");
        RunCommand(()=>Session.Select("gentle_guardian"));Only("gentle_guardian");
        RunCommand(()=>Session.Select(null));Only();
        StageAttackDemonstration();Only("hooded_assassin","nigel");
        Session.Characters["red_gentle_guardian"].SetTacticalPosition(new(14,11));
        _fixtures.Single(f=>f.CharacterId=="red_gentle_guardian").ReconcileToRuntime(Layout.Board);
        _attackTargetId="red_gentle_guardian";RefreshMatchHud();Only("hooded_assassin","red_gentle_guardian");
        _attackTargetId="gentle_guardian";RefreshMatchHud();Only("hooded_assassin"); // Friendly basic attacks remain illegal.
        ToggleAttackMode();Only("hooded_assassin");
        UpdateMovementOverlay(new(15,10));Session.Characters["time_weaver"].TakeDamage(1);RefreshMatchHud();Only("hooded_assassin");
        _abilitiesButton.EmitSignal(Button.SignalName.Pressed);ChooseAbilityInUi(1);_previewAbility.EmitSignal(Button.SignalName.Pressed);
        _abilityTarget="nigel";RefreshMatchHud();Only("hooded_assassin","nigel");
        var targetView=_fixtures.Single(f=>f.CharacterId=="nigel");var parent=targetView.GetParent();
        parent.RemoveChild(targetView);RefreshMatchHud();Only("hooded_assassin");parent.AddChild(targetView);RefreshMatchHud();Only("hooded_assassin","nigel");
        var selectedView=_fixtures.Single(f=>f.CharacterId=="hooded_assassin");parent.RemoveChild(selectedView);RefreshMatchHud();Only();parent.AddChild(selectedView);RefreshMatchHud();Only("hooded_assassin","nigel");
        CancelAbilityUi();RefreshMatchHud();Only("hooded_assassin");
        ToggleAttackMode();_attackTargetId="nigel";RefreshMatchHud();Only("hooded_assassin","nigel");
        _confirmAttack.EmitSignal(Button.SignalName.Pressed);Only(); // Completion clears selection/target; floating damage must not reopen bars.
        ResetMatch();StageAttackDemonstration();
        var target=Session.Characters["nigel"];target.TakeDamage(target.CurrentHealth);RefreshMatchHud();Only("hooded_assassin");
        var selected=Session.Characters["hooded_assassin"];selected.TakeDamage(selected.CurrentHealth);RefreshMatchHud();Only();
        ResetMatch();Only();GD.Print("HEALTH BAR VISIBILITY VERIFIED: selected/validated target only, switching, cancellation/completion, death, removed views, hover/damage exclusions and reset.");
    }
}
