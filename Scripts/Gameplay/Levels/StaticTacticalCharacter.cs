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
    private readonly Queue<Cell> _travel=new();
    private RectangularBoard? _travelBoard;
    private MeshInstance3D _selection = null!;
    private StandardMaterial3D _teamMaterial = null!;
    private readonly List<(GeometryInstance3D Geometry,Material? Material)> _modelGeometry=new();
    private StandardMaterial3D _defeatMaterial=null!;
    public MobaUnitPresentation? Presentation=>RuntimeCharacter is { } c?MobaUnitPresentation.Capture(c):null;
    public override void _Ready()
    {
        if(Engine.IsEditorHint())return;
        _selection=GetNode<MeshInstance3D>("Selection");
        var model=GetNodeOrNull<Node3D>(ModelNodePath);
        if(model is not null)Freeze(model);
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
        var indicator=GetNode<MeshInstance3D>("TeamIndicator");
        if(_teamMaterial is null){_teamMaterial=(StandardMaterial3D)indicator.MaterialOverride.Duplicate();indicator.MaterialOverride=_teamMaterial;}
        // A picking-only body, following TacticalUnit's existing layer-2 convention. It blocks no movement.
        if(GetNodeOrNull<StaticBody3D>("RuntimeSelectionBody") is not null)return;
        var body=new StaticBody3D {Name="RuntimeSelectionBody",CollisionLayer=2,CollisionMask=0};
        body.SetMeta("fixture_id",CharacterId);
        body.AddChild(new CollisionShape3D {Position=new(0,.85f,0),Shape=new BoxShape3D {Size=new(.85f,1.7f,.85f)}});
        AddChild(body);
    }
    public void RefreshRuntime(bool selected,bool eligible,bool visibleToObserver=true,string smokeStatus="")
    {
        if(RuntimeCharacter is not { } c)return;
        var state=MobaUnitPresentation.Capture(c);
        VisibleToObserver=visibleToObserver;GetNode<Node3D>(ModelNodePath).Visible=visibleToObserver;
        GetNode<MeshInstance3D>("TeamIndicator").Visible=visibleToObserver;GetNode<Label3D>("Nameplate").Visible=visibleToObserver;
        Select(selected&&!state.Defeated&&visibleToObserver);
        GetNode<StaticBody3D>("RuntimeSelectionBody").CollisionLayer=state.Pickable&&visibleToObserver?2u:0u;
        foreach(var entry in _modelGeometry)entry.Geometry.MaterialOverride=state.Defeated?_defeatMaterial:entry.Material;
        var color=c.TeamId==1 ? new Color(.12f,.65f,1) : new Color(1,.22f,.3f);
        _teamMaterial.AlbedoColor=c.IsDefeated ? Colors.DimGray : c.HasActedThisRound ? color.Darkened(.65f) : eligible ? color.Lightened(.3f) : color;
        GetNode<Label3D>("Nameplate").Text=$"{c.Name}\nP{c.TeamId} · {(Moving?"MOVING":c.IsDefeated ? "DEFEATED" : c.HasActedThisRound ? "USED" : "READY")}"+(smokeStatus!=""?$"\n{smokeStatus}":"");
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
    }
    public void AdvanceMovement(double delta)
    {
        if(_travelBoard is null)return;
        float budget=(float)Math.Max(0,delta)*4f;
        while(_travel.TryPeek(out var cell)&&budget>0)
        {
            var target=Center(cell,_travelBoard);float distance=GlobalPosition.DistanceTo(target);
            if(distance<=budget){GlobalPosition=target;budget-=distance;_travel.Dequeue();}
            else {GlobalPosition=GlobalPosition.MoveToward(target,budget);break;}
        }
    }
    public void ReconcileToRuntime(RectangularBoard board)
    {_travel.Clear();GlobalPosition=Center(Cell,board);}
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
