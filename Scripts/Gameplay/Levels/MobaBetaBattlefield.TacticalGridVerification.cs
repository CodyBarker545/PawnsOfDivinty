using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Cell=PawnsOfDivinity.Core.Abilities.GridPosition;

public partial class MobaBetaBattlefield
{
    // Explicit verification launch only. Never saves a battlefield scene or gameplay fixture.
    private async void VerifyTacticalGrid()
    {
        try
        {
            void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("TACTICAL GRID: "+why);}
            async Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
            async Task Press(Key key,bool echo=false)
            {
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=key,Keycode=key,Unicode=(uint)char.ToLower((char)key),Pressed=true,Echo=echo});
                await Frame();
                Input.ParseInputEvent(new InputEventKey {PhysicalKeycode=key,Keycode=key,Pressed=false});
                await Frame();
            }
            async Task Capture(string suffix)
            {
                if(!OS.GetCmdlineUserArgs().Contains("--capture-grid-review"))return;
                await Frame();await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/stage30d1_{Name}_{suffix}.png");
            }
            bool expected=!SceneFilePath.Contains("CliffsideArena");
            Check(_grid.Visible==expected,"scene-level initial visibility");
            string logical=JsonSerializer.Serialize(Layout.Source);
            var otherMeshes=FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>!_grid.IsAncestorOf(m)).ToArray();
            var visibleBefore=otherMeshes.ToDictionary(m=>m.GetInstanceId(),m=>m.IsVisibleInTree());
            _grid.Visible=false;await Capture("off");_grid.Visible=expected;
            await Press(Key.G);Check(_grid.Visible!=expected,"physical G press through viewport input");
            bool first=_grid.Visible;
            await Press(Key.G);Check(_grid.Visible==expected,"second G press");
            await Press(Key.G,true);Check(_grid.Visible==expected,"key-repeat ignored");
            _UnhandledInput(new InputEventKey {PhysicalKeycode=Key.G,Pressed=true,CtrlPressed=true});
            Check(_grid.Visible==expected,"unbound modifier chord ignored");
            Check(otherMeshes.All(m=>m.IsVisibleInTree()==visibleBefore[m.GetInstanceId()]),"non-grid visuals unchanged");
            Check(JsonSerializer.Serialize(Layout.Source)==logical,"logical grid data unchanged");
            foreach(Control field in new Control[]{new LineEdit(),new TextEdit()})
            {
                field.Position=new(400,70);field.Size=new(180,40);_hud.Root.AddChild(field);field.GrabFocus();await Frame();
                Check(TacticalTextInputFocused(),"text focus detected");
                _UnhandledInput(new InputEventKey {PhysicalKeycode=Key.G,Keycode=Key.G,Pressed=true});
                await Press(Key.G);Check(_grid.Visible==expected,"text input does not toggle");
                field.ReleaseFocus();field.QueueFree();await Frame();
            }
            var binding=InputMap.ActionGetEvents(TacticalGridAction);
            InputMap.ActionEraseEvents(TacticalGridAction);InputMap.ActionAddEvent(TacticalGridAction,new InputEventKey {PhysicalKeycode=Key.J});
            await Press(Key.G);Check(_grid.Visible==expected,"old key is not hard-coded");
            await Press(Key.J);Check(_grid.Visible!=expected,"rebound key works");
            InputMap.ActionEraseEvents(TacticalGridAction);foreach(var input in binding)InputMap.ActionAddEvent(TacticalGridAction,input);
            _grid.Visible=true;await Capture("on");_grid.Visible=false;
            int gridSurfaces;
            if(_batchedTacticalGrid is not null)
            {
                Check(_batchedTacticalGrid.Mesh.GetSurfaceCount()==1,"shared one-surface renderer");
                Check(_grid.GetChildren().OfType<MeshInstance3D>().Count(m=>m.Visible)==1,"legacy lines hidden independently");
                var mesh=_batchedTacticalGrid.Mesh;var points=mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                Check(points.Length==_batchedGridVertexCount&&points.Length>0,"nonempty compiled grid");
                // Check against independent transformed authored bounds, not a second copied map definition.
                foreach(var source in _grid.GetChildren().OfType<MeshInstance3D>().Where(m=>m!=_batchedTacticalGrid))
                {
                    var box=_grid.GlobalTransform.AffineInverse()*source.GlobalTransform*source.GetAabb();
                    var low=box.Position+Vector3.Up*TacticalGridLift;var high=box.End+Vector3.Up*TacticalGridLift;
                    Check(points.Any(p=>p.DistanceTo(low)<.0002f)&&points.Any(p=>p.DistanceTo(high)<.0002f),"authored line corners/elevation preserved: "+source.Name);
                }
                var bounds=mesh.GetAabb();float half=(float)Layout.Board.CellSize/2;
                Check(Mathf.Abs(bounds.Position.X-(CellPosition(new(0,0)).X-half))<.06f,"board left edge");
                Check(Mathf.Abs(bounds.End.X-(CellPosition(new(Layout.Board.Width-1,0)).X+half))<.06f,"board right edge");
                Check(bounds.Position.Y<-1.7f&&bounds.End.Y>.02f,"lower/upper elevations retained");
                gridSurfaces=mesh.GetSurfaceCount();
            }
            else
            {
                Check(_legacyGridMeshCount==1,"beta retains its existing shared renderer");gridSurfaces=1;
            }
            RunCommand(()=>Session.Select("hooded_assassin"));UpdateMovementOverlay(null);
            Check(_movementTiles.Values.Any(m=>m.IsVisibleInTree()),"movement ranges visible with grid off");
            Check(_fixtures.Single(f=>f.CharacterId=="hooded_assassin").GetNode<MeshInstance3D>("Selection").IsVisibleInTree(),"selection ring independent");
            var destination=_reachable.Keys.First(c=>_hoverPlan is null&&c!=Session.SelectedCharacter!.TacticalPosition);
            UpdateMovementOverlay(destination);_hover.Visible=true;_hover.Position=CellPosition(destination,.025f);
            Check(_hoverPlan is not null&&_movementTiles.Values.Any(m=>m.Visible&&m.MaterialOverride==_pathMaterial),"planned route visible");
            int rangeCount=_movementTiles.Values.Count(m=>m.IsVisibleInTree());
            await Press(Key.G);await Press(Key.G);
            Check(!_grid.Visible&&_movementTiles.Values.Count(m=>m.IsVisibleInTree())==rangeCount&&_hover.Visible,"toggle preserves active indicators");
            await Capture("selected_off");
            float normalSize=_camera.Size;var normalPosition=_camera.Position;var normalFocus=_focus;
            _focus=CellPosition(Session.SelectedCharacter!.TacticalPosition!.Value);_camera.Size=19;UpdateCamera();await Capture("selected_close_off");
            _focus=normalFocus;_camera.Size=normalSize;_camera.Position=normalPosition;UpdateCamera();
            StageAttackDemonstration();RefreshMatchHud();
            Check(_attackMode&&_attackRings["nigel"].IsVisibleInTree(),"target ring works with grid off");
            await Capture("target_off");
            float size=_camera.Size;
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.WheelUp,Pressed=true,Position=_hud.BattlefieldRect.GetCenter()});
            Check(_camera.Size<size,"camera wheel preserved");
            float yaw=_yaw;_UnhandledInput(new InputEventMouseMotion {ButtonMask=MouseButtonMask.Middle,Relative=new(20,0)});
            Check(_yaw!=yaw,"camera rotation preserved");
            var focus=_focus;AdvanceCamera(.3,1,0,0);Check(_focus.DistanceTo(focus)>.1f,"camera pan preserved");
            Check(JsonSerializer.Serialize(Layout.Source)==logical,"verification does not edit authored tiles");
            var result=new {passed=true,scene=SceneFilePath,defaultVisible=expected,afterFirstPress=first,logicalDataIdentical=true,textInputIgnored=true,rebindWorks=true,authoredAlignment=true,movementIndicatorsIndependent=true,selectionAndTargeting=true,cameraInputPreserved=true,legacyMeshes=_legacyGridMeshCount,gridSurfaces};
            var path=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--grid-report="))?.Split('=',2)[1];
            if(path is not null)System.IO.File.WriteAllText(path,JsonSerializer.Serialize(result));
            GD.Print("TACTICAL GRID VERIFICATION PASSED: "+JsonSerializer.Serialize(result));GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
