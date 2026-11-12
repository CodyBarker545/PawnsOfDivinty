using Godot;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity;
using PawnsOfDivinity.Gameplay;
using CoreCell = PawnsOfDivinity.Core.Abilities.GridPosition;

/// <summary>A visual binding and animation queue. Gameplay position and health belong to Character.</summary>
public partial class TacticalUnit : Node3D
{
    public BattlefieldUnitBinding Binding { get; private set; } = null!;
    public string CharacterId => Binding.CharacterId;
    public string DisplayName => Binding.Capture().Name;
    public Vector2I Cell => Binding.Capture().Position is { } p ? new(p.X, p.Y) : default;
    public bool Moving => _path.Count > 0;
    private readonly Queue<CoreCell> _path = new();
    private Battlefield _board = null!;
    private AnimationPlayer? _animation;
    private MeshInstance3D _marker = null!;
    private Label3D _label = null!;
    private string _playing = "";

    public void Initialize(Battlefield board, BattlefieldUnitBinding binding, string? modelPath, Color color)
    {
        _board = board; Binding = binding;
        Position = board.CellPosition(Cell);
        Node3D visual;
        if (modelPath is not null && ResourceLoader.Exists(modelPath))
        {
            visual = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
            visual.Scale = Vector3.One * 1.35f;
        }
        else
        {
            visual = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .28f, Height = 1.5f }, Position = new(0, .8f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = color.Lightened(.15f) } };
        }
        AddChild(visual); _animation = FindAnimation(visual);
        var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("unit", this);
        body.AddChild(new CollisionShape3D { Position = new(0, 1.1f, 0), Shape = new BoxShape3D { Size = new(.8f, 2.3f, .8f) } });
        AddChild(body);
        _marker = new MeshInstance3D { Position = new(0, .025f, 0),
            Mesh = new CylinderMesh { TopRadius = .48f, BottomRadius = .48f, Height = .018f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(_marker);
        _label = new Label3D { Position = new(0, 2.9f, 0), PixelSize = .005f, FontSize = 25,
            Modulate = color.Lightened(.6f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
        AddChild(_label); Refresh(); Play("Idle");
    }
    private static AnimationPlayer? FindAnimation(Node root)
    {
        if (root is AnimationPlayer player) return player;
        foreach (Node child in root.GetChildren()) { var found = FindAnimation(child); if (found is not null) return found; }
        return null;
    }
    public void Select(bool selected) => _marker.Scale = Vector3.One * (selected ? 1.17f : .82f);
    private void Play(string clip)
    {
        if (_animation is null || _playing == clip) return;
        foreach (var name in _animation.GetAnimationList())
            if (name.ToString().EndsWith(clip))
            { _animation.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear; _animation.Play(name); _playing = clip; return; }
    }
    public void Animate(MovementActionResult result)
    {
        if (result.ActorId != CharacterId) return;
        _path.Clear();
        if (result.TraversedPath.Count > 1)
        {
            Position = _board.CellPosition(new(result.TraversedPath[0].X, result.TraversedPath[0].Y));
            foreach (var cell in result.TraversedPath.Skip(1)) _path.Enqueue(cell);
            Play("Walk");
        }
        else Refresh();
    }
    public void Refresh()
    {
        var state = Binding.Capture();
        _label.Text = $"{state.Name}\n{state.HP}/{state.MaxHP} HP · {state.ShieldHP} shield";
        _label.Modulate = state.Defeated ? Colors.Gray : Colors.White;
        if (!Moving && state.Position is { } p) Position = _board.CellPosition(new(p.X, p.Y));
    }
    public void Advance(double delta)
    {
        float remaining = (float)delta * 3.5f;
        while (_path.Count > 0 && remaining > 0)
        {
            var cell = _path.Peek(); var target = _board.CellPosition(new(cell.X, cell.Y));
            var direction = target - Position; direction.Y = 0;
            float distance = direction.Length();
            if (distance > .001f) Rotation = new(0, Mathf.Atan2(direction.X, direction.Z), 0);
            if (distance <= remaining)
            {
                Position = target; _path.Dequeue(); remaining -= distance;
                if (!Moving) { Play("Idle"); Refresh(); _board.Arrived(this); }
            }
            else
            {
                Position += direction.Normalized() * remaining;
                Position = new(Position.X, _board.Map.SurfaceHeight(Position) + .045f, Position.Z);
                remaining = 0;
            }
        }
    }
    public override void _Process(double delta) { Advance(delta); Refresh(); }
}
