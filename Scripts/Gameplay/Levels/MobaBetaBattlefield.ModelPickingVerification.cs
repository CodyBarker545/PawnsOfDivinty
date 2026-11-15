using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class MobaBetaBattlefield
{
    private async Task VerifyModelPickingSmoke()
    {
        void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Model targeting: "+why);}
        var window=GetWindow();var saved=window.Size;var scale=window.ContentScaleSize;
        ResetMatch();StageAttackDemonstration();_focus=CellPosition(new(14,10));_yaw=0;_camera.Size=23;UpdateCamera();
        foreach(var resolution in new[]{new Vector2I(1280,720),new Vector2I(1920,1080)})
        {
            window.ContentScaleSize=Vector2I.Zero;window.Size=resolution;
            for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            foreach(var target in Session.Characters.Values.Where(c=>c.TeamId==2))
            {
                var original=Layout.Fixtures.Single(f=>f.CharacterId==target.Id).Cell;target.SetTacticalPosition(new(15,10));
                var fixture=_fixtures.Single(f=>f.CharacterId==target.Id);fixture.ReconcileToRuntime(Layout.Board);
                await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
                var shape=fixture.GetNode<CollisionShape3D>("RuntimeSelectionBody/ModelPickShape");
                var box=(BoxShape3D)shape.Shape;
                foreach(float height in new[]{.55f,.85f})
                {
                    var point=shape.GlobalPosition+Vector3.Up*box.Size.Y*(height-.5f);
                    _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=WorldToScreen(point)});_PhysicsProcess(0);
                    Check(_attackTargetId==target.Id,$"{fixture.DisplayName} torso/head is selectable at {resolution}");
                    Check(_combatHud.HealthVisible(target.Id)&&_attackRings[target.Id].Visible,"actual chosen target feedback");
                    Check(target.CurrentHealth==target.BaseStats.MaxHealth&&!Session.Activations.MainActionUsed,"model click only previews, never attacks");
                }
                target.SetTacticalPosition(original);fixture.ReconcileToRuntime(Layout.Board);
            }
        }
        window.ContentScaleSize=scale;window.Size=saved;
        for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        ResetMatch();ResetCamera();GD.Print("MODEL TARGETING VERIFIED: all five opposing model fixtures, torso/head clicks, actual mouse handler, target feedback and no action spending at 720p/1080p.");
    }
}
