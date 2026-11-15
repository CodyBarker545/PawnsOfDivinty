using Godot;
using System;
using System.Linq;
using PawnsOfDivinity.Core.Statuses;

public partial class MobaBetaBattlefield
{
    private bool _capturePolish;
    private void VerifyPolishSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("HUD polish: "+why);}
        ResetMatch();
        foreach(var b in new[]{_moveButton,_attack,_abilitiesButton,_transfer,_endActivation,_previewAbility,_confirmAttack,_confirmAbility,_confirmTransfer})
        {Check(b.Disabled,"empty selection disables "+b.Text);b.EmitSignal(Button.SignalName.Pressed);}
        Check(Session.SelectedFixtureId is null&&Session.Activations.ActiveCharacter is null&&!_attackMode&&!_abilitiesOpen&&!_transferMode,"disabled inputs cannot enter modes or spend actions");
        RunCommand(()=>Session.Select("hooded_assassin"));Check(_moveButton.ButtonPressed&&!_attack.ButtonPressed,"default movement mode accent");
        var view=_fixtures.Single(f=>f.CharacterId=="hooded_assassin");var selector=view.GetNode<MeshInstance3D>("Selection");
        Check(selector.Visible,"existing selected ring visible");var start=view.Position;view.Position+=new Vector3(0,1.25f,0);
        Check(Mathf.IsEqualApprox(selector.GlobalPosition.Y,view.GlobalPosition.Y+.065f),"selected ring inherits fixture elevation");view.Position=start;
        var routes=Session.ReachableDestinations();var hover=routes.First().Key;UpdateMovementOverlay(hover);
        Check(_movementTiles.Where(e=>e.Value.Visible).Select(e=>e.Key).ToHashSet().SetEquals(routes.Keys),"subdued visuals retain exact authoritative reachable set");
        Check(_reachableMaterial.AlbedoColor.A<_pathMaterial.AlbedoColor.A&&_hoverPlan?.Cost==routes[hover].Cost,"hover/path stronger than reachable and actual cost unchanged");
        _attack.EmitSignal(Button.SignalName.Pressed);Check(_attack.ButtonPressed&&!_moveButton.ButtonPressed&&_attackRings.Values.All(r=>!r.Visible),"attack accent without displaying merely attackable targets");
        ToggleAttackMode();StageAttackDemonstration();
        Check(_attackRings.Count(e=>e.Value.Visible)==1&&_attackRings["nigel"].Visible,"one actual target ring");
        Check(_attackRings["nigel"].MaterialOverride!=selector.MaterialOverride,"selected and hostile target ring styles differ");
        var targetView=_fixtures.Single(f=>f.CharacterId=="nigel");var targetStart=targetView.Position;targetView.Position+=new Vector3(.2f,1.5f,.1f);
        Check(_attackRings["nigel"].GlobalPosition.IsEqualApprox(targetView.GlobalPosition+new Vector3(0,.075f,0)),"target ring follows movement and fixture elevation");targetView.Position=targetStart;
        Session.Characters["nigel"].SetTacticalPosition(new(29,19));RefreshMatchHud();Check(_attackRings.Values.All(r=>!r.Visible),"newly invalid target loses its ring");
        Session.Characters["nigel"].SetTacticalPosition(new(15,10));RefreshMatchHud();ToggleAttackMode();
        Check(!_attack.ButtonPressed&&_moveButton.ButtonPressed&&_attackRings.Values.All(r=>!r.Visible),"cancellation clears old command/target highlights");
        _abilitiesButton.EmitSignal(Button.SignalName.Pressed);ChooseAbilityInUi(0);Check(_abilitiesButton.ButtonPressed&&!_moveButton.ButtonPressed,"ability inspection accent");
        Session.SelectedCharacter!.Statuses.ApplyTimedStatus(StatusType.Silence,2);RefreshMatchHud();
        Check(!_abilitiesButton.Disabled&&_previewAbility.Disabled,"inspectable panel retained while status blocks ability execution");
        _previewAbility.EmitSignal(Button.SignalName.Pressed);Check(!_abilityMode&&Session.TargetedAbilityId is null&&!Session.Activations.MainActionUsed,"disabled preview cannot execute");
        Session.SelectedCharacter.Statuses.RemoveStatus(StatusType.Silence);RefreshMatchHud();Check(!_previewAbility.Disabled,"production status removal restores preview");
        _moveButton.EmitSignal(Button.SignalName.Pressed);Check(!_abilitiesButton.ButtonPressed&&_moveButton.ButtonPressed,"switching back to movement clears ability accent");
        var moved=Session.MoveTo(Session.ReachableDestinations().First().Key);Session.AcknowledgeVisualMovement(moved.RequestId,moved.FinalPosition!.Value);view.ReconcileToRuntime(Layout.Board);RefreshMatchHud();
        Check(_moveButton.Disabled&&!_moveButton.ButtonPressed&&!_attack.Disabled,"movement exhaustion clears accent without consuming Main Action");
        _endActivation.EmitSignal(Button.SignalName.Pressed);RunCommand(()=>Session.Select("nigel"));Check(_moveButton.ButtonPressed,"Red unit movement accent");
        Check(_fixtures.Single(f=>f.CharacterId=="nigel").GetNode<MeshInstance3D>("Selection").Visible,"Red selected ring");
        ResetMatch();StageCastleTransferDemonstration();_transfer.EmitSignal(Button.SignalName.Pressed);
        Check(_transferMode&&_transfer.ButtonPressed&&!_moveButton.ButtonPressed&&!_attack.ButtonPressed,"legal castle transfer has its own accent");
        _transfer.EmitSignal(Button.SignalName.Pressed);Check(!_transfer.ButtonPressed,"transfer cancellation clears accent");
        ResetMatch();RunCommand(()=>Session.Select("hooded_assassin"));
        // Hidden targets must not leak either HP or a warm indicator, even through stale preview IDs.
        var enemy=Session.Characters["red_hooded_assassin"];enemy.SetTacticalPosition(new(20,10));
        Session.Smoke.Place("polish-hidden",enemy.Id,new(20,10),Session.Round,_=>true);
        _attack.EmitSignal(Button.SignalName.Pressed);_attackTargetId=enemy.Id;RefreshMatchHud();
        Check(!Session.IsVisibleToPlayer(enemy.Id)&&!_combatHud.HealthVisible(enemy.Id)&&!_attackRings[enemy.Id].Visible,"hidden enemy cannot leak health or target ring");
        ResetMatch();GD.Print("HUD POLISH VERIFIED: contextual indicators, chosen-target-only rings, inherited elevation, unchanged routes, active/disabled commands, status gates and hidden enemy privacy.");
    }
    private void CapturePolish()
    {
        string[] names={"move_720","attack_720","abilities_720","move_1080","attack_1080","abilities_1080"};
        string file=$"res://artifacts/moba_polish_{names[_captureStep]}.png";GetViewport().GetTexture().GetImage().SavePng(file);GD.Print("POLISH CAPTURE: "+file);
        switch(++_captureStep)
        {
            case 1:StageAttackDemonstration();break;
            case 2:ToggleAttackMode();_abilitiesButton.EmitSignal(Button.SignalName.Pressed);break;
            case 3:CancelAbilityUi();RefreshMatchHud();GetWindow().ContentScaleSize=Vector2I.Zero;GetWindow().Size=new(1920,1080);break;
            case 4:ToggleAttackMode();_attackTargetId="nigel";RefreshMatchHud();break;
            case 5:ToggleAttackMode();_abilitiesButton.EmitSignal(Button.SignalName.Pressed);break;
            default:GetTree().Quit();break;
        }
    }
}
