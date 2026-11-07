using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Battlefield : Node3D
{
    public TacticalMap Map { get; private set; } = null!;
    private readonly List<TacticalUnit> _units = new();
    private readonly Dictionary<Vector2I, MeshInstance3D> _pathMarkers = new();
    private Camera3D _camera = null!;
    private TacticalUnit? _selected;
    private Label _status = null!;
    private Label _objectiveStatus = null!;
    private StandardMaterial3D _objectiveMaterial = null!;
    private MeshInstance3D _grid = null!;
    private Vector2? _pendingClick;
    private bool _smokeRequested;
    private bool _captureRequested;
    private double _captureElapsed;
    private string _objectiveOwner = "Unclaimed";
    private static readonly Color Blue = new(.12f, .74f, .95f);
    private static readonly Color Red = new(.96f, .3f, .36f);
    private static readonly Color Neutral = new(.92f, .68f, .25f);

    public override void _Ready()
    {
        var level = GetNode<Node3D>("StartingLevel");
        Map = new TacticalMap(level);
        _camera = GetNode<Camera3D>("Camera3D");
        _objectiveMaterial = (StandardMaterial3D)level.GetNode<MeshInstance3D>("Objective/CaptureRing").MaterialOverride.Duplicate();
        level.GetNode<MeshInstance3D>("Objective/CaptureRing").MaterialOverride = _objectiveMaterial;
        level.GetNode<MeshInstance3D>("Objective/Beacon").MaterialOverride = _objectiveMaterial;
        CreateHud();
        CreateGrid();
        Spawn("Assassin", "res://Assets/Characters/Assassin/Assassin.glb", level.GetNode<Marker3D>("SpawnPoints/Assassin"), Blue);
        Spawn("Ashveil", "res://Assets/Characters/Ashveil/Ashveil.glb", level.GetNode<Marker3D>("SpawnPoints/Ashveil"), Red);
        UpdateObjective();
        _smokeRequested = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _captureRequested = OS.GetCmdlineUserArgs().Contains("--capture-level");
    }

    private void Spawn(string name, string path, Marker3D spawn, Color color)
    {
        var cell = Map.WorldToCell(spawn.GlobalPosition);
        if (!Map.Walkable(cell)) throw new InvalidOperationException($"{name} spawn is blocked.");
        var unit = new TacticalUnit { Name = name };
        AddChild(unit);
        unit.Initialize(this, name, path, cell, color);
        unit.Select(false);
        _units.Add(unit);
    }

    public Vector3 CellPosition(Vector2I cell) => Map.CellPosition(cell);

    private static StandardMaterial3D OverlayMaterial(Color color) => new()
    {
        AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private Vector3 CellCorner(Vector2I cell, float x, float z)
    {
        var center = CellPosition(cell);
        var point = new Vector3(center.X + x * TacticalMap.CellSize, 0, center.Z + z * TacticalMap.CellSize);
        point.Y = Map.SurfaceHeight(point) + .065f;
        return point;
    }

    private void CreateGrid()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Lines);
        for (int x = 0; x < TacticalMap.Size; x++)
            for (int z = 0; z < TacticalMap.Size; z++)
            {
                var cell = new Vector2I(x, z);
                if (!Map.Walkable(cell)) continue;
                // Subdivided edges follow the terrain instead of cutting through hills.
                for (int i = 0; i < 5; i++)
                {
                    float a = -.48f + i * .192f, b = a + .192f;
                    surface.AddVertex(CellCorner(cell, a, -.48f));
                    surface.AddVertex(CellCorner(cell, b, -.48f));
                    surface.AddVertex(CellCorner(cell, -.48f, a));
                    surface.AddVertex(CellCorner(cell, -.48f, b));
                }
            }
        _grid = new MeshInstance3D { Name = "TacticalGrid", Mesh = surface.Commit(),
            MaterialOverride = OverlayMaterial(new Color(.72f, .84f, .7f, .14f)) };
        AddChild(_grid);
    }

    public void ClearTile(Vector2I cell)
    {
        if (_pathMarkers.Remove(cell, out var marker)) marker.QueueFree();
    }

    public void ShowPath(List<Vector2I> path)
    {
        foreach (var marker in _pathMarkers.Values) marker.QueueFree();
        _pathMarkers.Clear();
        foreach (var cell in path)
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 4; z++)
                {
                    float a = -.46f + x * .23f, b = -.46f + z * .23f;
                    Vector3[] p = { CellCorner(cell,a,b), CellCorner(cell,a+.23f,b),
                        CellCorner(cell,a,b+.23f), CellCorner(cell,a+.23f,b+.23f) };
                    foreach (int i in new[] {0,2,1,1,2,3}) surface.AddVertex(p[i]);
                }
            var color = cell == path[^1] ? new Color(1,.74f,.2f,.6f) : new Color(.1f,.65f,.9f,.35f);
            var mesh = new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = OverlayMaterial(color) };
            AddChild(mesh);
            _pathMarkers.Add(cell, mesh);
        }
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);
        var header = new PanelContainer { Position = new Vector2(20, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
        var background = new StyleBoxFlat { BgColor = new Color(.035f,.065f,.08f,.92f),
            BorderColor = new Color(.36f,.45f,.4f), BorderWidthLeft = 3,
            ContentMarginLeft = 16, ContentMarginRight = 20, ContentMarginTop = 10, ContentMarginBottom = 10 };
        header.AddThemeStyleboxOverride("panel", background);
        canvas.AddChild(header);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(column);
        var title = new Label { Text = "OATHSTONE CROSSING", MouseFilter = Control.MouseFilterEnum.Ignore };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(.95f,.85f,.62f));
        column.AddChild(title);
        var subtitle = new Label { Text = "Claim the shrine. Choose the causeway or a flank bridge.", MouseFilter = Control.MouseFilterEnum.Ignore };
        subtitle.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(subtitle);
        _objectiveStatus = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _objectiveStatus.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(_objectiveStatus);
        var footer = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 20, OffsetRight = -20, OffsetTop = -82, OffsetBottom = -18,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        footer.AddThemeStyleboxOverride("panel", background);
        canvas.AddChild(footer);
        var footerColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        footer.AddChild(footerColumn);
        _status = new Label { Text = "Select Assassin (west) or Ashveil (east), then click a destination.", MouseFilter = Control.MouseFilterEnum.Ignore };
        _status.AddThemeFontSizeOverride("font_size", 15);
        footerColumn.AddChild(_status);
        var hint = new Label { Text = "WASD  Pan    •    Wheel  Zoom    •    G  Grid    •    Home  Recenter    •    Right-click  Deselect", MouseFilter = Control.MouseFilterEnum.Ignore };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", new Color(.65f,.75f,.76f));
        footerColumn.AddChild(hint);
    }

    private void Select(TacticalUnit? unit)
    {
        _selected = unit;
        foreach (var candidate in _units) candidate.Select(candidate == unit);
        _status.Text = unit == null ? "Select a character to move." : $"{unit.DisplayName} selected — reach one of the four tiles around the Oathstone.";
    }

    private bool MoveSelected(Vector2I cell)
    {
        if (_selected == null || _units.Any(u => u.Moving)) return false;
        bool moved = _selected.MoveTo(cell, _units.Where(u => u != _selected).Select(u => u.Cell).ToHashSet());
        _status.Text = moved ? $"{_selected.DisplayName} advancing to {cell}." : "That route is blocked. Use clear ground, the causeway or a bridge.";
        return moved;
    }

    public void Arrived(TacticalUnit unit)
    {
        UpdateObjective();
        _status.Text = TacticalMap.IsObjective(unit.Cell) ? $"{unit.DisplayName} reached the Oathstone. {_objectiveOwner}." : $"{unit.DisplayName} arrived at {unit.Cell}.";
    }

    private void UpdateObjective()
    {
        var contenders = _units.Where(u => TacticalMap.IsObjective(u.Cell)).ToList();
        if (contenders.Count > 1) _objectiveOwner = "Contested";
        else if (contenders.Count == 1) _objectiveOwner = contenders[0].DisplayName;
        else if (_objectiveOwner == "Contested") _objectiveOwner = "Unclaimed";
        var color = _objectiveOwner == "Assassin" ? Blue : _objectiveOwner == "Ashveil" ? Red : Neutral;
        _objectiveMaterial.AlbedoColor = color;
        _objectiveMaterial.Emission = color * .45f;
        _objectiveStatus.Text = $"OATHSTONE  ·  {_objectiveOwner.ToUpperInvariant()}";
        _objectiveStatus.AddThemeColorOverride("font_color", color.Lightened(.15f));
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.G) _grid.Visible = !_grid.Visible;
            if (key.PhysicalKeycode == Key.Home) _camera.Position = new Vector3(16,24,48);
        }
        if (input is not InputEventMouseButton mouse || !mouse.Pressed) return;
        if (mouse.ButtonIndex == MouseButton.Left) _pendingClick = mouse.Position;
        if (mouse.ButtonIndex == MouseButton.Right) Select(null);
        if (mouse.ButtonIndex == MouseButton.WheelUp) _camera.Size = Mathf.Max(12,_camera.Size-1.5f);
        if (mouse.ButtonIndex == MouseButton.WheelDown) _camera.Size = Mathf.Min(38,_camera.Size+1.5f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_smokeRequested) { _smokeRequested = false; SmokeTest(); return; }
        if (_pendingClick is not Vector2 position) return;
        _pendingClick = null;
        var from = _camera.ProjectRayOrigin(position);
        var direction = _camera.ProjectRayNormal(position);
        var query = PhysicsRayQueryParameters3D.Create(from,from+direction*200,2);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit["collider"].AsGodotObject().HasMeta("unit"))
            Select(hit["collider"].AsGodotObject().GetMeta("unit").AsGodotObject() as TacticalUnit);
        else if (Map.RayCell(from,direction,out var cell)) MoveSelected(cell);
    }

    public override void _Process(double delta)
    {
        float x = (Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0);
        float z = (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0);
        var next = _camera.Position + new Vector3(x,0,z).Normalized()*(float)delta*8;
        _camera.Position = new Vector3(Mathf.Clamp(next.X,6,26),next.Y,Mathf.Clamp(next.Z,38,58));
        if (_captureRequested && (_captureElapsed += delta) > 3)
        {
            _captureRequested = false;
            GetViewport().GetTexture().GetImage().SavePng("res://artifacts/oathstone_crossing.png");
            GD.Print("LEVEL PREVIEW SAVED");
            GetTree().Quit();
        }
    }

    public void SmokeTest()
    {
        try
        {
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
            Check(GridPathfinder.Find(new(0,0),new(-1,0),16,new()) == null,"Off-map destination accepted");
            Check(GridPathfinder.Find(new(0,0),new(2,0),3,new(){new(1,0)})?.Count == 4,"Obstacle path failed");
            Check(Map.BlockedCells().Count > 20,"Water and prop blocking are missing");
            Check(!Map.Walkable(new(2,2)),"Tree cell is passable");
            Check(!Map.Walkable(new(6,4)),"Open river cell is passable");
            var treeMeshes = GetNode("StartingLevel/LevelProps/Tree_0_0").FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().ToList();
            Check(treeMeshes.Count > 0 && treeMeshes.All(m => m.GetSurfaceOverrideMaterial(0) is StandardMaterial3D),"Saved prop materials are missing");
            Check(CellPosition(new(2,2)).Y > CellPosition(new(1,7)).Y + .5f,"Raised terrain missing");
            var routeA = Map.FindPath(new(1,7),new(7,7),new());
            var routeB = Map.FindPath(new(14,8),new(8,8),new());
            Check(routeA != null && routeB != null && routeA.Count == routeB.Count,"Starting sides have unequal or missing objective routes");
            var north = Map.FindPath(new(5,2),new(11,2),new());
            var south = Map.FindPath(new(10,13),new(4,13),new());
            Check(north != null && south != null,"Flank crossings are unreachable");
            Check(north!.Any(c => Mathf.Abs(Map.SurfaceHeight(CellPosition(c)) - .3f) < .001f),"North route does not use bridge");
            Check(south!.Any(c => Mathf.Abs(Map.SurfaceHeight(CellPosition(c)) - .3f) < .001f),"South route does not use bridge");
            foreach (var cell in new[] { new Vector2I(1,7), new Vector2I(7,7), new Vector2I(8,8), new Vector2I(7,2) })
            {
                var world = CellPosition(cell);
                var screen = _camera.UnprojectPosition(world);
                Check(Map.RayCell(_camera.ProjectRayOrigin(screen),_camera.ProjectRayNormal(screen),out var picked) && picked == cell,"Terrain or bridge picking failed");
            }
            for (int i = 0; i < _units.Count; i++)
            {
                var unit = _units[i];
                Check(unit.HasMovementAnimations,"Imported idle/walk clips missing");
                var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(unit.GlobalPosition+Vector3.Up*4,unit.GlobalPosition-Vector3.Up,2));
                Check(hit.Count>0 && hit["collider"].AsGodotObject().GetMeta("unit").AsGodotObject()==unit,"Unit collider failed");
                Select(unit);
                var other = _units.First(u=>u!=unit);
                var original = other.Cell;
                Check(!MoveSelected(other.Cell),"Occupied destination accepted");
                var goal = i == 0 ? new Vector2I(7,7) : new Vector2I(8,8);
                Check(MoveSelected(goal),"Objective route rejected");
                unit.Advance(.4);
                Check(Mathf.Abs(unit.Position.Y-Map.SurfaceHeight(unit.Position)-.045f)<.005f,"Character floats or sinks during travel");
                unit.Advance(20);
                Check(unit.Cell==goal && !unit.Moving,"Unit failed to reach objective");
                Check(other.Cell==original,"Unselected unit moved");
                Check(_objectiveOwner==(i==0 ? "Assassin" : "Contested"),"Objective claim/contest failed");
            }
            // Exercise real movement across a bridge after the objective test.
            Select(_units[0]);
            Check(MoveSelected(new(5,2)),"Route from shrine to north approach missing");
            _units[0].Advance(30);
            Check(MoveSelected(new(11,2)),"Bridge movement rejected");
            _units[0].Advance(30);
            Check(_units[0].Cell==new Vector2I(11,2) && _objectiveOwner=="Ashveil","Bridge arrival or objective ownership failed");
            GD.Print("GODOT C# SMOKE TEST PASSED: Terrain3D heights, balanced spawns, both bridges, blocked water/props, picking, animations, terrain-following movement, occupancy and objective claim/contest");
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
