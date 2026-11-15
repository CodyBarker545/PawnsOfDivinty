using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Standalone Godot integration check and optional rendered six-character pose gallery.</summary>
public partial class CharacterAnimationVerification : Node3D
{
    public override async void _Ready()
    {
        try
        {
            var names=new[]{"AssassinTextured","GentleGuardian","TimeWeaver","Nigel","Colossus","SpiritGuardian"};
            var models=new List<(Node3D Model,CharacterAnimationController Controller,Skeleton3D Skeleton,AnimationPlayer Player)>();
            var camera=new Camera3D {Position=new(3.2f,7,14),Projection=Camera3D.ProjectionType.Orthogonal,Size=9,Current=true};
            AddChild(camera);camera.LookAt(new(3.2f,1,-2.3f));
            var environment=new WorldEnvironment {Environment=new Godot.Environment {BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new(.12f,.14f,.18f),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=Colors.White,AmbientLightEnergy=.2f}};
            AddChild(environment);
            var light=new DirectionalLight3D {RotationDegrees=new(-40,-30,0),LightEnergy=.7f};AddChild(light);
            AddChild(new MeshInstance3D {Position=new(3.2f,-.035f,-2.3f),Mesh=new BoxMesh {Size=new(13,.05f,13)},MaterialOverride=new StandardMaterial3D {AlbedoColor=new(.23f,.26f,.3f)}});
            for(int i=0;i<names.Length;i++)
            {
                var model=GD.Load<PackedScene>($"res://Assets/Characters/MobaBeta/{names[i]}.glb").Instantiate<Node3D>();
                model.Position=new(i%3*3.2f,0,-(i/3)*4.6f);AddChild(model);
                var controller=new CharacterAnimationController();AddChild(controller);
                Require(controller.Initialize(model),$"{names[i]} controller initializes");
                var skeleton=model.FindChildren("*","Skeleton3D",true,false).OfType<Skeleton3D>().Single();
                var player=skeleton.GetChildren().OfType<AnimationPlayer>().Single();
                models.Add((model,controller,skeleton,player));
                AddChild(new Label3D {Position=model.Position+new Vector3(0,3,0),Text=names[i],FontSize=32,PixelSize=.006f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled});
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            foreach(var state in Enum.GetValues<CharacterAnimationState>())
            {
                foreach(var (model,controller,skeleton,player) in models)
                {
                    controller.Reset();
                    if(state==CharacterAnimationState.Walk)controller.SetMoving(true);
                    else if(state!=CharacterAnimationState.Idle)controller.Request(state);
                    Require(controller.State==state,$"{model.SceneFilePath}: selects {state}");
                    var transform=model.Transform;
                    player.Advance(.2);
                    var bone=skeleton.FindBone("mixamorig_LeftArm");
                    if(bone<0)bone=skeleton.FindBone("LeftArm");
                    Require(bone>=0,"Verification requires a mapped upper arm");
                    var before=skeleton.GetBonePoseRotation(bone);
                    player.Advance(.35);
                    Require(before.AngleTo(skeleton.GetBonePoseRotation(bone))>.00001f,$"{state}: skeletal pose advances");
                    Require(model.Transform==transform,$"{state}: model transform preserved");
                    if(state==CharacterAnimationState.Death)player.Advance(1.9);
                    // Freeze the sampled pose while rendering; no direct edits to skeleton or mesh.
                    player.SpeedScale=0;
                }
                if(DisplayServer.GetName()!="headless")
                {
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng($"res://artifacts/animation_{state}.png");
                }
                foreach(var (_,controller,_,player) in models)
                {
                    player.SpeedScale=1;
                    if(state is CharacterAnimationState.Attack or CharacterAnimationState.HitReaction)
                    {player.Advance(5);Require(controller.State==CharacterAnimationState.Idle,$"{state}: returns to Idle");}
                    if(state==CharacterAnimationState.Death)
                    {player.Advance(5);Require(controller.State==CharacterAnimationState.Death,"Death holds final pose");}
                }
            }
            GD.Print("CHARACTER ANIMATION VERIFICATION PASS: six original models, five states, skeletal motion, action completion, transform isolation.");
            GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Require(bool condition,string message)
    {if(!condition)throw new InvalidOperationException(message);}
}
