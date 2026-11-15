using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity;
using PawnsOfDivinity.Core.Characters;
using PawnsOfDivinity.Gameplay.Levels;
using Cell = PawnsOfDivinity.Core.Abilities.GridPosition;

/// <summary>Serialized fixture; authored transforms and original model resources are never rebuilt.</summary>
[Tool]
public partial class StaticTacticalCharacter : Node3D
{
    [Export] public string CharacterId { get; set; } = "";
    [Export] public string DefinitionId { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public int TeamId { get; set; }
    [Export] public NodePath ModelNodePath { get; set; } = new("Facing/Model");
    public BetaCharacterFixture Fixture { get; private set; } = null!;
    public string? AuthoredModelPath => GetNodeOrNull<Node3D>(ModelNodePath)?.SceneFilePath;
    public bool UsedFallback => false;
    public Character? RuntimeCharacter { get; private set; }
    public Cell Cell=>RuntimeCharacter?.TacticalPosition??Fixture.Cell;
    public bool Moving=>_travel.Count>0;
    public bool VisibleToObserver {get;private set;}=true;
    public Vector3 PickingAnchor=>GetNode<CollisionShape3D>("RuntimeSelectionBody/ModelPickShape").GlobalPosition;
    private readonly Queue<Cell> _travel=new();
    private RectangularBoard? _travelBoard;
    private MeshInstance3D _selection = null!;
    private StandardMaterial3D _teamMaterial = null!;
    private readonly List<(GeometryInstance3D Geometry,Material? Material)> _modelGeometry=new();
    private StandardMaterial3D _defeatMaterial=null!;
    public CharacterAnimationController AnimationController {get;private set;}=null!;
    private int? _lastHealth;
    private bool _wasDefeated;
    private Transform3D _authoredFacing;
    public MobaUnitPresentation? Presentation=>RuntimeCharacter is { } c?MobaUnitPresentation.Capture(c):null;
    public override void _Ready()
    {
        if(Engine.IsEditorHint())return;
        _authoredFacing=GetNode<Node3D>("Facing").Transform;
        _selection=GetNode<MeshInstance3D>("Selection");
        // Refine the existing selector in-place; no second selected-unit indicator.
        _selection.Mesh=new TorusMesh {InnerRadius=.52f,OuterRadius=.60f,Rings=24,RingSegments=8};
        _selection.MaterialOverride=new StandardMaterial3D {AlbedoColor=TeamId==1?new(.15f,.92f,1):new(.68f,.55f,1),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
        _selection.Position=new(0,.065f,0);
        var model=GetNodeOrNull<Node3D>(ModelNodePath);
        if(model is not null)Freeze(model);
        AnimationController=new CharacterAnimationController {Name="CharacterAnimationController"};
        AddChild(AnimationController);
        if(model is not null)AnimationController.Initialize(model);
        if(model is not null)RememberGeometry(model);
        _defeatMaterial=new StandardMaterial3D {AlbedoColor=Colors.DimGray,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded};
    }
    public void Bind(BetaCharacterFixture fixture)
    {
        if(string.IsNullOrWhiteSpace(AuthoredModelPath))
            throw new InvalidOperationException($"{GetPath()}: restore the original model at {ModelNodePath}; placeholders are not supported.");
        Fixture=fixture;
    }
    public void Select(bool selected) => _selection.Visible=selected;
    public void BindRuntime(Character character)
    {
        if(character.Id!=CharacterId || character.TeamId!=TeamId || character.TacticalPosition!=Fixture.Cell)
            throw new InvalidOperationException($"{CharacterId}: scene/runtime identity, team or position mismatch.");
        RuntimeCharacter=character;
        GetNode<Node3D>("Facing").Transform=_authoredFacing;
        _lastHealth=character.CurrentHealth;_wasDefeated=character.IsDefeated;
        AnimationController.Reset();
        var indicator=GetNode<MeshInstance3D>("TeamIndicator");
        if(_teamMaterial is null){_teamMaterial=(StandardMaterial3D)indicator.MaterialOverride.Duplicate();indicator.MaterialOverride=_teamMaterial;}
        // A picking-only body, following TacticalUnit's existing layer-2 convention. It blocks no movement.
        if(GetNodeOrNull<StaticBody3D>("RuntimeSelectionBody") is not null)return;
        var body=new StaticBody3D {Name="RuntimeSelectionBody",CollisionLayer=2,CollisionMask=0};
        body.SetMeta("fixture_id",CharacterId);
        // Imported characters vary in height/width. Cover the actual transformed model, including
        // its head, rather than relying on a fixed 1.7-unit proxy that can stop below the mesh.
        var min=new Vector3(-.425f,0,-.425f);var max=new Vector3(.425f,1.7f,.425f);
        var inverse=GlobalTransform.AffineInverse();
        foreach(var mesh in _modelGeometry.Select(e=>e.Geometry).OfType<MeshInstance3D>().Where(m=>m.Mesh is not null))
        {
            var bounds=mesh.GetAabb();var relative=inverse*mesh.GlobalTransform;
            for(int i=0;i<8;i++){var point=relative*bounds.GetEndpoint(i);min=min.Min(point);max=max.Max(point);}
        }
        var margin=Vector3.One*.06f;
        body.AddChild(new CollisionShape3D {Name="ModelPickShape",Position=(min+max)/2,Shape=new BoxShape3D {Size=max-min+margin*2}});
        AddChild(body);
    }
    public void RefreshRuntime(bool selected,bool eligible,bool visibleToObserver=true,string smokeStatus="")
    {
        if(RuntimeCharacter is not { } c)return;
        var state=MobaUnitPresentation.Capture(c);
        if(state.Defeated&&!_wasDefeated)AnimationController.Request(CharacterAnimationState.Death);
        else if(!state.Defeated&&_wasDefeated)AnimationController.Reset();
        else if(!state.Defeated&&_lastHealth is { } hp&&state.HP<hp)AnimationController.Request(CharacterAnimationState.HitReaction);
        _lastHealth=state.HP;_wasDefeated=state.Defeated;
        VisibleToObserver=visibleToObserver;GetNode<Node3D>(ModelNodePath).Visible=visibleToObserver;
        GetNode<MeshInstance3D>("TeamIndicator").Visible=visibleToObserver;GetNode<Label3D>("Nameplate").Visible=visibleToObserver;
        Select(selected&&!state.Defeated&&visibleToObserver);
        GetNode<StaticBody3D>("RuntimeSelectionBody").CollisionLayer=state.Pickable&&visibleToObserver?2u:0u;
        foreach(var entry in _modelGeometry)entry.Geometry.MaterialOverride=state.Defeated?_defeatMaterial:entry.Material;
        var color=c.TeamId==1 ? new Color(.12f,.65f,1) : new Color(1,.22f,.3f);
        _teamMaterial.AlbedoColor=c.IsDefeated ? Colors.DimGray : c.HasActedThisRound ? color.Darkened(.65f) : eligible ? color.Lightened(.3f) : color;
        GetNode<Label3D>("Nameplate").Text=c.IsDefeated?"† DEFEATED":selected?c.Name+(smokeStatus!=""?$" · {smokeStatus}":""):"";
    }
    private void RememberGeometry(Node node)
    {
        if(node is GeometryInstance3D geometry)_modelGeometry.Add((geometry,geometry.MaterialOverride));
        foreach(Node child in node.GetChildren())RememberGeometry(child);
    }
    /// <summary>Visual playback only: domain position was committed by MovementActionExecutor already.</summary>
    public void PresentMovement(MovementActionResult result,RectangularBoard board)
    {
        if(Moving||RuntimeCharacter is null||result.ActorId!=RuntimeCharacter.Id||result.FinalPosition!=RuntimeCharacter.TacticalPosition)
            throw new InvalidOperationException("Movement view/result binding mismatch.");
        _travelBoard=board;
        if(result.TraversedPath.Count<2){ReconcileToRuntime(board);return;}
        GlobalPosition=Center(result.TraversedPath[0],board);
        foreach(var cell in result.TraversedPath.Skip(1))_travel.Enqueue(cell);
        AnimationController.SetMoving(Moving);
    }
    public void AdvanceMovement(double delta)
    {
        if(_travelBoard is null)return;
        float budget=(float)Math.Max(0,delta)*4f;
        while(_travel.TryPeek(out var cell)&&budget>0)
        {
            var target=Center(cell,_travelBoard);float distance=GlobalPosition.DistanceTo(target);
            FaceTowards(target);
            if(distance<=budget){GlobalPosition=target;budget-=distance;_travel.Dequeue();}
            else {GlobalPosition=GlobalPosition.MoveToward(target,budget);break;}
        }
        AnimationController.SetMoving(Moving);
    }
    public void ReconcileToRuntime(RectangularBoard board)
    {_travel.Clear();GlobalPosition=Center(Cell,board);AnimationController.SetMoving(false);}
    public void PresentAttack(Vector3 target)
    {FaceTowards(target);AnimationController.Request(CharacterAnimationState.Attack);}
    private void FaceTowards(Vector3 target)
    {
        var direction=target-GlobalPosition;
        if(direction.LengthSquared()<.00001f)return;
        var facing=GetNode<Node3D>("Facing");
        facing.Rotation=new(0,Mathf.Atan2(direction.X,direction.Z),0);
    }
    private static Vector3 Center(Cell cell,RectangularBoard board)
    {var p=board.CellCenter(cell);return new((float)p.X,0,(float)p.Z);}
    private static void Freeze(Node node)
    {
        if(node is AnimationPlayer player){player.Stop();player.Active=false;}
        if(node is AnimationTree tree)tree.Active=false;
        if(node is CollisionObject3D body){body.CollisionLayer=0;body.CollisionMask=0;}
        node.ProcessMode=ProcessModeEnum.Disabled;
        foreach(Node child in node.GetChildren())Freeze(child);
    }
}
