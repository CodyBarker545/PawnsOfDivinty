using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class TacticalUnit : Node3D
{
    public string DisplayName { get; private set; } = "Unit";
    public Vector2I Cell { get; private set; }
    public bool Moving => _path.Count > 0;
    public bool HasMovementAnimations => _animation != null &&
        new[] { "Idle", "Walk" }.All(clip => _animation.GetAnimationList().Any(name => name.ToString().EndsWith(clip)));
    private readonly Queue<Vector2I> _path = new();
    private Battlefield _board = null!;
    private AnimationPlayer? _animation;
    private MeshInstance3D _marker = null!;
    private string _playing = "";

    public void Initialize(Battlefield board, string name, string modelPath, Vector2I cell, Color markerColor)
    {
        _board = board; DisplayName = name; Cell = cell;
        Position = board.CellPosition(cell);
        var visual = GD.Load<PackedScene>(modelPath).Instantiate<Node3D>();
        visual.Scale = Vector3.One * 1.35f;
        AddChild(visual);
        _animation = FindAnimation(visual);
        var body = new StaticBody3D { CollisionLayer = 2, CollisionMask = 0 };
        body.SetMeta("unit", this);
        body.AddChild(new CollisionShape3D { Position = new Vector3(0,1.25f,0), Shape = new BoxShape3D { Size = new Vector3(.9f,2.65f,.9f) } });
        AddChild(body);
        _marker = new MeshInstance3D { Position = new Vector3(0,.025f,0),
            Mesh = new CylinderMesh { TopRadius = .52f, BottomRadius = .52f, Height = .018f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = markerColor, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(_marker);
        var label = new Label3D { Text = name.ToUpperInvariant(), Position = new Vector3(0,2.85f,0),
            PixelSize = .007f, FontSize = 32, Modulate = markerColor.Lightened(.45f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
        AddChild(label);
        Rotation = new Vector3(0, cell.X < 8 ? Mathf.Pi / 2 : -Mathf.Pi / 2, 0);
        Play("Idle");
    }

    private static AnimationPlayer? FindAnimation(Node root)
    {
        if (root is AnimationPlayer player) return player;
        foreach (Node child in root.GetChildren()) { var found = FindAnimation(child); if (found != null) return found; }
        return null;
    }

    public void Select(bool selected) => _marker.Scale = Vector3.One * (selected ? 1.17f : .82f);

    private void Play(string clip)
    {
        if (_animation == null || _playing == clip) return;
        foreach (var name in _animation.GetAnimationList())
        {
            if (name.ToString().EndsWith(clip))
            {
                _animation.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
                _animation.Play(name); _playing = clip; return;
            }
        }
    }

    public bool MoveTo(Vector2I target, HashSet<Vector2I> blocked)
    {
        if (Moving) return false;
        var route = _board.Map.FindPath(Cell, target, blocked);
        if (route == null || route.Count == 0) return false;
        foreach (var point in route) _path.Enqueue(point);
        _board.ShowPath(route); Play("Walk"); return true;
    }

    public void Advance(double delta)
    {
        float remaining = (float)delta * 3.5f;
        while (_path.Count > 0 && remaining > 0)
        {
            var cell = _path.Peek(); var target = _board.CellPosition(cell);
            var direction = target - Position; direction.Y = 0;
            var distance = direction.Length();
            if (distance > .001f) Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
            if (distance <= remaining)
            {
                Position = target; Cell = _path.Dequeue(); remaining -= distance; _board.ClearTile(cell);
                if (!Moving) { Play("Idle"); _board.Arrived(this); }
            }
            else
            {
                Position += direction.Normalized() * remaining;
                Position = new Vector3(Position.X, _board.Map.SurfaceHeight(Position) + .045f, Position.Z);
                remaining = 0;
            }
        }
    }

    public override void _Process(double delta) => Advance(delta);
}
