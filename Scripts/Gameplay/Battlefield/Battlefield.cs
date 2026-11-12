using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity;
using PawnsOfDivinity.Core.Combat;
using PawnsOfDivinity.Core.Turns;
using PawnsOfDivinity.Gameplay;
using CoreCell = PawnsOfDivinity.Core.Abilities.GridPosition;

/// <summary>Input, HUD and visual reconciliation for one production-backed development encounter.</summary>
public partial class Battlefield : Node3D
{
    [Export] public bool DevelopmentMovementPolicy { get; set; }
    [Export] public bool DevelopmentOpenTargeting { get; set; }
    [Export] public bool DevelopmentInterruptedMovementSpent { get; set; }
    public TacticalMap Map { get; private set; } = null!;
    public BattlefieldSession Session { get; private set; } = null!;
    private readonly List<TacticalUnit> _units = new();
    private readonly Dictionary<Vector2I, MeshInstance3D> _pathMarkers = new();
    private readonly List<CoreCell> _route = new();
    private Camera3D _camera = null!;
    private TacticalUnit? _selected;
    private Label _status = null!, _round = null!, _details = null!, _events = null!;
    private Button _activate = null!, _move = null!, _confirm = null!, _attack = null!, _clear = null!, _end = null!, _phase = null!;
    private MeshInstance3D _grid = null!;
    private Vector2? _pendingClick;
    private bool _buildingPath, _choosingAttack, _smokeRequested, _captureRequested;
    private double _captureElapsed;
    private long _request;
    private Vector3 _cameraFocus = new(16, 0, 16);
    private float _cameraYaw;
    private bool Busy => _units.Any(u => u.Moving);

    public override void _Ready()
    {
        try
        {
            Map = new TacticalMap(GetNode<Node3D>("StartingLevel"));
            _camera = GetNode<Camera3D>("Camera3D");
            _smokeRequested = OS.GetCmdlineUserArgs().Any(a => a is "--smoke-test" or "--bridge-smoke-test");
            _captureRequested = OS.GetCmdlineUserArgs().Contains("--capture-level");
            Session = new(Map, new(DevelopmentMovementPolicy, DevelopmentOpenTargeting,
                DevelopmentInterruptedMovementSpent ? true : null), _smokeRequested ? new SystemRandomRollProvider(new Random(1)) : null);
            CreateHud(); CreateGrid();
            foreach (var character in Session.Characters.Values)
            {
                var unit = new TacticalUnit { Name = character.Id };
                AddChild(unit);
                unit.Initialize(this, new(Session, character.Id), character.Definition!.Id == "hooded_assassin"
                    ? "res://Assets/Characters/Assassin/Assassin.glb" : null,
                    character.TeamId == 1 ? new(.12f, .74f, .95f) : new(.96f, .3f, .36f));
                unit.Select(false); _units.Add(unit);
            }
            UpdateCamera(); RefreshHud();
            GD.Print($"CORE BRIDGE READY: {TacticalMap.Size}x{TacticalMap.Size}, {_units.Count} registered characters. {Session.Rules.Description}");
        }
        catch (Exception error)
        {
            GD.PushError($"Battlefield setup failed: {error}");
            if (_status is not null) _status.Text = "Battlefield setup failed; inspect the runtime log.";
            SetProcess(false); SetPhysicsProcess(false); SetProcessUnhandledInput(false);
            if (_smokeRequested) GetTree().Quit(1);
        }
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

    public void ShowPath(List<Vector2I> path, bool legal = true)
    {
        foreach (var marker in _pathMarkers.Values) marker.QueueFree();
        _pathMarkers.Clear();
        foreach (var cell in path.Distinct())
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
            var color = !legal ? new Color(.95f,.18f,.2f,.55f) : cell == path[^1] ? new Color(1,.74f,.2f,.6f) : new Color(.1f,.65f,.9f,.35f);
            var mesh = new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = OverlayMaterial(color) };
            AddChild(mesh);
            _pathMarkers.Add(cell, mesh);
        }
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer(); AddChild(canvas);
        StyleBoxFlat Background() => new() { BgColor = new(.035f,.065f,.08f,.94f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
        var top = new PanelContainer { Position = new(14, 10), Size = new(1252, 88) };
        top.AddThemeStyleboxOverride("panel", Background()); canvas.AddChild(top);
        var header = new VBoxContainer(); top.AddChild(header);
        _round = new Label(); _round.AddThemeFontSizeOverride("font_size", 19); header.AddChild(_round);
        var assumptions = new Label { Text = Session.Rules.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new(1200, 0) };
        assumptions.AddThemeFontSizeOverride("font_size", 12); assumptions.Modulate = new(1,.83f,.43f); header.AddChild(assumptions);
        var bottom = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, AnchorRight = 1,
            OffsetLeft = 14, OffsetRight = -14, OffsetTop = -184, OffsetBottom = -12 };
        bottom.AddThemeStyleboxOverride("panel", Background()); canvas.AddChild(bottom);
        var column = new VBoxContainer(); bottom.AddChild(column);
        _details = new Label { Text = "Select a unit to inspect it. Selection does not start an activation." }; column.AddChild(_details);
        var buttons = new HBoxContainer(); column.AddChild(buttons);
        Button Add(string text, Action pressed)
        {
            var button = new Button { Text = text }; button.Pressed += () => RunUi(pressed); buttons.AddChild(button); return button;
        }
        _activate = Add("Start Activation", () => { SetStatus(Session.StartSelectedActivation().Reason); ClearPath(); });
        _move = Add("Move", BeginPath);
        _confirm = Add("Confirm Move", ConfirmMovement);
        _attack = Add("Attack", BeginAttack);
        _clear = Add("Clear Path", ClearPath);
        _end = Add("End Activation", () => { SetStatus(Session.EndActivation().Reason); ClearPath(); });
        _phase = Add("Advance Phase", () => { SetStatus(Session.AdvancePhase().Reason); ClearPath(); });
        _status = new Label { Text = "Advance the displayed round-start phases to Preparation, then choose an actor.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart }; column.AddChild(_status);
        var hint = new Label { Text = "WASD pan · Q/E or middle-drag rotate · wheel zoom · Home recenter · G grid · right-click inspect off" };
        hint.AddThemeFontSizeOverride("font_size", 12); column.AddChild(hint);
        var log = new PanelContainer { Position = new(916, 108), Size = new(350, 200) };
        log.AddThemeStyleboxOverride("panel", Background()); canvas.AddChild(log);
        _events = new Label { Text = "Combat / round events", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new(325, 0) }; _events.AddThemeFontSizeOverride("font_size", 12); log.AddChild(_events);
    }
    private void RunUi(Action action)
    {
        try { if (!Busy) action(); }
        catch (Exception error) { SetStatus($"Failed: {error.Message}. Actual core state retained; no retry."); GD.PushError(error.ToString()); }
        RefreshHud();
    }
    private void SetStatus(string message) => _status.Text = message;
    private void Select(TacticalUnit? unit)
    {
        _selected = unit; Session.Select(unit?.CharacterId); ClearPath();
        foreach (var view in _units) view.Select(view == unit);
        SetStatus(unit is null ? "Selection cleared." : $"Inspecting {unit.DisplayName}. Use Start Activation to act."); RefreshHud();
    }
    private void ClearPath()
    {
        _route.Clear(); _buildingPath = false; _choosingAttack = false; ShowPath(new());
    }
    private void BeginPath()
    {
        ClearPath();
        if (_selected is null || Session.Activations.ActiveCharacter?.Id != _selected.CharacterId) return;
        _route.Add(Session.Characters[_selected.CharacterId].TacticalPosition!.Value); _buildingPath = true;
        SetStatus("Click adjacent tiles to append an explicit route, then Confirm Move. Clear Path restarts it.");
    }
    private void AppendStep(Vector2I cell)
    {
        _route.Add(new(cell.X, cell.Y));
        var preview = Session.PreviewMovement(new("path-preview", _selected!.CharacterId, Session.Lifecycle.CurrentRound, _route));
        ShowPath(_route.Select(p => new Vector2I(p.X,p.Y)).ToList(), preview.Outcome == MovementOutcome.Ready);
        SetStatus($"{preview.Outcome}: {preview.Reason} · cost {preview.ApprovedCost?.ToString() ?? "unresolved"}/{Session.Characters[_selected.CharacterId].EffectiveMovement}");
    }
    private void ConfirmMovement()
    {
        if (!_buildingPath || _selected is null) return;
        var result = Session.ExecuteMovement(new($"ui-move-{++_request}", _selected.CharacterId, Session.Lifecycle.CurrentRound, _route));
        _selected.Animate(result);
        _buildingPath = false; _route.Clear();
        ShowPath(result.TraversedPath.Skip(1).Select(p => new Vector2I(p.X,p.Y)).ToList());
        SetStatus($"{result.Outcome}: {result.Reason}. Final {result.FinalPosition}; movement spent: {result.MovementConsumed}.");
    }
    private void BeginAttack()
    {
        ClearPath(); _choosingAttack = true;
        var actor = Session.Activations.ActiveCharacter;
        if (actor is null) return;
        var targets = Session.Characters.Values.Where(c => c.TeamId != actor.TeamId &&
            Session.PreviewAttack(actor.Id,c.Id).Outcome == BattlefieldOutcome.Ready).Select(c => c.TacticalPosition!.Value);
        ShowPath(targets.Select(p => new Vector2I(p.X,p.Y)).ToList());
        SetStatus("Click an enemy to submit an ordinary attack. Highlighted targets pass the current production preview.");
    }
    private void AttackTarget(TacticalUnit target)
    {
        var actor = Session.Activations.ActiveCharacter;
        if (actor is null) return;
        var result = Session.ExecuteAttack($"ui-attack-{++_request}", actor.Id, target.CharacterId);
        ClearPath(); SetStatus($"{result.Outcome}: {result.Reason}");
        foreach (var view in _units) view.Refresh(); RefreshHud();
    }
    public void Arrived(TacticalUnit unit) { ShowPath(new()); RefreshHud(); }
    private void RefreshHud()
    {
        if (Session is null || _round is null) return;
        var manager = Session.Activations;
        _round.Text = $"PAWNS OF DIVINITY · 14×14 DEVELOPMENT SLICE · Round {Session.Lifecycle.CurrentRound} · {Session.Lifecycle.CurrentPhase} · " +
            $"Team {manager.ActiveCharacter?.TeamId.ToString() ?? manager.NextTeamId?.ToString() ?? "—"} · Active: {manager.ActiveCharacter?.Name ?? "none"}";
        bool active = _selected is not null && manager.ActiveCharacter?.Id == _selected.CharacterId;
        bool commands = !Busy && !Session.RequiresManualRecovery;
        if (_selected is not null)
        {
            var state = _selected.Binding.Capture();
            _details.Text = $"{state.Name} · Team {state.TeamId} · HP {state.HP}/{state.MaxHP} · Shield {state.ShieldHP} · Position {state.Position} · MOV {state.Movement}\n" +
                $"Statuses: {string.Join(", ", state.Statuses.DefaultIfEmpty("none"))} · Movement {(active && manager.CanMove ? "available" : "unavailable")} · " +
                $"Main {(active && manager.CanUseMainAction ? "available" : "unavailable")} · Utility {(active && manager.CanUseUtilityAction ? "available" : "unavailable")} · Activated: {state.HasActed}";
            _activate.Disabled = !commands || !state.CanActivate;
        }
        else { _details.Text = "Select any character to inspect HP, shields, position and eligibility. Selection spends no activation."; _activate.Disabled = true; }
        _move.Disabled = !commands || !active || !manager.CanMove;
        _attack.Disabled = !commands || !active || !manager.CanUseMainAction;
        _confirm.Disabled = !commands || !_buildingPath || _route.Count < 2;
        _clear.Disabled = Busy || (!_buildingPath && !_choosingAttack && _pathMarkers.Count == 0);
        _end.Disabled = !commands || manager.ActiveCharacter is null;
        _phase.Disabled = !commands || (Session.Lifecycle.CurrentPhase == RoundPhase.AlternatingActivations && !manager.IsRoundComplete);
        _events.Text = "EVENTS\n" + string.Join("\n", Session.EventLog.TakeLast(6));
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.PhysicalKeycode == Key.G) _grid.Visible = !_grid.Visible;
            if (key.PhysicalKeycode == Key.Home) { _cameraFocus = new(16,0,16); _cameraYaw = 0; _camera.Size = 29; UpdateCamera(); }
        }
        if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Middle) != 0)
        { _cameraYaw -= motion.Relative.X * .008f; UpdateCamera(); }
        if (input is not InputEventMouseButton mouse || !mouse.Pressed) return;
        if (mouse.ButtonIndex == MouseButton.Left && !Busy) _pendingClick = mouse.Position;
        if (mouse.ButtonIndex == MouseButton.Right) Select(null);
        if (mouse.ButtonIndex == MouseButton.WheelUp) _camera.Size = Mathf.Max(12,_camera.Size-1.5f);
        if (mouse.ButtonIndex == MouseButton.WheelDown) _camera.Size = Mathf.Min(38,_camera.Size+1.5f);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (_smokeRequested) { _smokeRequested = false; SmokeTest(); return; }
        if (_pendingClick is not Vector2 position || Busy) return;
        _pendingClick = null;
        RunUi(() =>
        {
            var from = _camera.ProjectRayOrigin(position); var direction = _camera.ProjectRayNormal(position);
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from,from+direction*200,2));
            if (hit.Count > 0 && hit["collider"].AsGodotObject().HasMeta("unit"))
            {
                var unit = hit["collider"].AsGodotObject().GetMeta("unit").AsGodotObject() as TacticalUnit;
                if (_choosingAttack && unit is not null) AttackTarget(unit); else if (!_buildingPath) Select(unit);
                else if (unit is not null) AppendStep(unit.Cell);
            }
            else if (Map.RayCell(from,direction,out var cell))
            {
                if (_buildingPath) AppendStep(cell);
                else if (_choosingAttack)
                {
                    var target = _units.FirstOrDefault(u => u.Cell == cell);
                    if (target is not null) AttackTarget(target); else SetStatus("No character target at that tile.");
                }
            }
        });
    }
    private void UpdateCamera()
    {
        _camera.Position = _cameraFocus + new Vector3(0,24,32).Rotated(Vector3.Up,_cameraYaw);
        _camera.LookAt(_cameraFocus,Vector3.Up);
    }
    public override void _Process(double delta)
    {
        float x = (Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0);
        float z = (Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0);
        var next = _cameraFocus + new Vector3(x,0,z).Rotated(Vector3.Up,_cameraYaw).Normalized()*(float)delta*8;
        _cameraFocus = new(Mathf.Clamp(next.X,6,26),0,Mathf.Clamp(next.Z,6,26));
        _cameraYaw += ((Input.IsPhysicalKeyPressed(Key.Q)?1:0)-(Input.IsPhysicalKeyPressed(Key.E)?1:0))*(float)delta;
        UpdateCamera(); RefreshHud();
        if (_captureRequested && (_captureElapsed += delta) > 3)
        { _captureRequested = false; GetViewport().GetTexture().GetImage().SavePng("res://artifacts/core_battlefield.png"); GD.Print("CORE BATTLEFIELD PREVIEW SAVED"); GetTree().Quit(); }
    }
    public void SmokeTest()
    {
        try
        {
            void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
            Check(TacticalMap.Size == 14 && _units.Count == 6,"Board or roster mismatch.");
            foreach (var view in _units)
            {
                Check(Map.Walkable(view.Cell),$"Illegal spawn {view.CharacterId} at {view.Cell}.");
                Check(view.Cell == Map.WorldToCell(view.Position),"View/core coordinate binding failed.");
                var screen = _camera.UnprojectPosition(view.Position);
                Check(Map.RayCell(_camera.ProjectRayOrigin(screen),_camera.ProjectRayNormal(screen),out var picked) && picked == view.Cell,"Terrain picking failed.");
            }
            for (int i = 0; i < 6; i++) Check(Session.AdvancePhase().Outcome == BattlefieldOutcome.Completed,"Round-start processing failed.");
            var assassin = _units.Single(u => u.CharacterId == "a_assassin"); Select(assassin);
            Check(Session.Activations.ActiveCharacter is null,"Selection started activation.");
            Check(Session.StartSelectedActivation().Outcome == BattlefieldOutcome.Completed,"Activation rejected.");
            CoreCell[] path = {new(4,6),new(5,6),new(6,6),new(7,6)};
            var preview = Session.PreviewMovement(new("runtime-move",assassin.CharacterId,1,path));
            Check(preview.Outcome == MovementOutcome.Ready,$"Movement preview: {preview.Reason}");
            var move = Session.ExecuteMovement(new("runtime-move",assassin.CharacterId,1,path)); assassin.Animate(move); assassin.Advance(30);
            Check(move.Outcome == MovementOutcome.Completed && assassin.Cell == new Vector2I(7,6),"Core movement failed.");
            Check(assassin.Position.DistanceTo(CellPosition(assassin.Cell)) < .005f,"Animation did not reconcile to core position.");
            int hp = Session.Characters["b_colossus"].CurrentHealth;
            var attack = Session.ExecuteAttack("runtime-attack",assassin.CharacterId,"b_colossus");
            Check(attack.Outcome == BattlefieldOutcome.Completed,$"Attack failed: {attack.Reason}");
            Check(Session.Characters["b_colossus"].CurrentHealth < hp && Session.Activations.ActiveCharacter is null,"Attack health/bookkeeping failed.");
            while (!Session.Activations.IsRoundComplete)
            {
                var next = Session.Characters.Values.First(c => c.TeamId == Session.Activations.NextTeamId && c.CanAct);
                Session.Select(next.Id); Check(Session.StartSelectedActivation().Outcome == BattlefieldOutcome.Completed,"Alternation failed.");
                Check(Session.EndActivation().Outcome == BattlefieldOutcome.Completed,"End activation failed.");
            }
            Check(Session.AdvancePhase().Outcome == BattlefieldOutcome.Completed,"Cleanup phase transition failed.");
            Check(Session.AdvancePhase().Outcome == BattlefieldOutcome.Completed && Session.LastCleanup?.Status == RoundCleanupStatus.Completed,"Production Cleanup failed.");
            Check(Session.AdvancePhase().Outcome == BattlefieldOutcome.Completed && Session.Lifecycle.CurrentRound == 2,"Next round failed.");
            for (int i=0;i<6;i++) Check(Session.AdvancePhase().Outcome == BattlefieldOutcome.Completed,"Next-round preparation failed.");
            Check(Session.Characters[assassin.CharacterId].CanAct && assassin.Cell == new Vector2I(7,6),"Preparation reset position or failed eligibility.");
            GD.Print("GODOT CORE BRIDGE SMOKE TEST PASSED: six bindings, 14x14 bounds, Terrain3D picking, selection, movement preview/animation, production attack, HP, alternation, Cleanup and next-round preparation.");
            GetTree().Quit(0);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
