using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity;
using PawnsOfDivinity.Gameplay.Levels;
using PawnsOfDivinity.Core.Characters.Definitions;
using Cell = PawnsOfDivinity.Core.Abilities.GridPosition;

/// <summary>Scene extraction, validation and presentation input. Never regenerates authored level geometry.</summary>
[Tool]
public partial class MobaBetaBattlefield : Node3D
{
    [Export] public BetaBoardDefinition BoardDefinition { get; set; } = null!;
    [Export(PropertyHint.Range,"1,2,1")] public int StartingPlayer { get; set; } = 1;
    [Export] public bool DevelopmentOpenTargeting { get; set; } = true;
    [ExportToolButton("Validate authored level")]
    public Callable ValidateLevel => Callable.From(ValidateAuthoring);
    public MobaBetaLayout Layout { get; private set; } = null!;
    public MobaHotseatSession Session { get; private set; } = null!;
    private readonly List<StaticTacticalCharacter> _fixtures = new();
    private Node3D _grid = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _hover = null!;
    private Label _status = null!;
    private Label _turn = null!, _details = null!, _message = null!, _readiness=null!;
    private Button _endActivation = null!;
    private Button _attack=null!,_confirmAttack=null!;
    private Button _resetMatch=null!;
    private MobaCombatHud _combatHud=null!;
    private Label _attackPreview=null!;
    private bool _attackMode;
    private string? _attackTargetId;
    private long _attackSequence;
    private readonly Dictionary<string,MeshInstance3D> _attackRings=new();
    private Vector2? _pendingSelection;
    private readonly Dictionary<Cell,MeshInstance3D> _movementTiles=new();
    private IReadOnlyDictionary<Cell,TacticalRoute> _reachable=new Dictionary<Cell,TacticalRoute>();
    private StandardMaterial3D _reachableMaterial=null!,_pathMaterial=null!,_invalidMaterial=null!;
    private MobaMovementPreview? _movementPreview;
    private StandardMaterial3D _neutralHoverMaterial=null!;
    private MobaMovementPlan? _hoverPlan;
    private Vector3 _focus;
    private float _yaw;
    private bool _capture, _smoke, _captureHotseat, _captureMovement, _captureAttack, _captureFeedback, _captureFive;
    private double _elapsed;
    private int _captureStep;
    private string? _snapshotPath;

    public override void _Ready()
    {
        if(Engine.IsEditorHint()){SetProcess(false);SetProcessUnhandledInput(false);return;}
        try
        {
            Layout=BetaSceneReader.Extract(this);
            Session=new MobaHotseatSession(Layout,StartingPlayer,developmentOpenTargeting:DevelopmentOpenTargeting,scoringProfile:DevelopmentScoring.Snapshot(),timerConfiguration:DevelopmentTimer.Snapshot(),assassinProfile:DevelopmentAssassin.Snapshot());
            _camera=GetNode<Camera3D>("Camera3D");_grid=GetNode<Node3D>("GridOverlay");
            foreach(var view in GetNode("CharacterFixtures").GetChildren().OfType<StaticTacticalCharacter>())
            {
                view.Bind(Layout.Fixtures.Single(f=>f.CharacterId==view.CharacterId));
                view.BindRuntime(Session.Characters[view.CharacterId]);_fixtures.Add(view);
                var ring=new MeshInstance3D {Name="RuntimeAttackTarget",Position=new(0,.075f,0),Visible=false,
                    Mesh=new TorusMesh {InnerRadius=.55f,OuterRadius=.65f},
                    MaterialOverride=new StandardMaterial3D {AlbedoColor=new(1,.12f,.12f),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}};
                view.AddChild(ring);_attackRings.Add(view.CharacterId,ring);
            }
            _hover=new MeshInstance3D {Name="RuntimeHover",Visible=false,
                Mesh=new BoxMesh {Size=new((float)Layout.Board.CellSize,.012f,(float)Layout.Board.CellSize)},
                MaterialOverride=new StandardMaterial3D {AlbedoColor=new(1,1,1,.3f),Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}};
            AddChild(_hover);ResetCamera();BuildHud();RefreshMatchHud();
            _neutralHoverMaterial=(StandardMaterial3D)_hover.MaterialOverride;
            _reachableMaterial=Overlay(new(.22f,1,.08f,.8f));
            _pathMaterial=Overlay(new(1,1,1,.9f));
            _invalidMaterial=Overlay(new(1,.15f,.2f,.4f));
            var args=OS.GetCmdlineUserArgs();
            _captureHotseat=args.Contains("--capture-hotseat");
            _captureMovement=args.Contains("--capture-movement");
            _captureAttack=args.Contains("--capture-attack");
            _captureFeedback=args.Contains("--capture-feedback");
            _captureFive=args.Contains("--capture-five");
            _captureObjectives=args.Contains("--capture-objectives");
            _captureTimer=args.Contains("--capture-timer");
            _captureAssassin=args.Contains("--capture-assassin");
            _capture=args.Contains("--capture-beta")||_captureHotseat||_captureMovement||_captureAttack||_captureFeedback||_captureFive||_captureObjectives||_captureTimer||_captureAssassin;
            if(_captureHotseat)
            {
                Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id);
                RefreshMatchHud();
            }
            if(_captureMovement)
            {
                if(Session.CurrentPlayer!=Session.Characters["nigel"].TeamId)
                {Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id);Session.EndSelectedActivation();}
                Session.Select("nigel");RefreshMatchHud();
                Session.Characters["nigel"].SetTacticalPosition(new(27,10));_fixtures.Single(f=>f.CharacterId=="nigel").ReconcileToRuntime(Layout.Board);
            }
            if(_captureAttack){StageAttackDemonstration();_focus=CellPosition(new(14,10));_camera.Size=17;UpdateCamera();}
            if(_captureFeedback)
            {
                StageAttackDemonstration();Session.Characters["nigel"].SetShieldHP(5);Session.Characters["hooded_assassin"].SetShieldHP(4);
                _focus=CellPosition(new(14,10));_camera.Size=19;UpdateCamera();ConfirmBasicAttack();_combatHud.ToggleHistory();
            }
            if(_captureObjectives)StageContestedCaptureDemonstration();
            if(_captureTimer)_captureTime=UseTimerSmokeClock();
            if(_captureAssassin)StageSmokePreview();
            _snapshotPath=args.FirstOrDefault(a=>a.StartsWith("--export-beta-layout="))?.Split('=',2)[1];
            _smoke=args.Contains("--beta-smoke-test")||_snapshotPath is not null;
            GD.Print($"MOBA AUTHORED BETA READY: {Layout.Board.Width}x{Layout.Board.Height}; {Layout.Obstacles.Count} blocked; {Layout.Terrain.Count-Layout.Obstacles.Count} walkable; {_fixtures.Count} original fixtures.");
        }
        catch(Exception error)
        {GD.PushError(error.ToString());SetProcess(false);SetProcessUnhandledInput(false);GetTree().Quit(1);}
    }
    public override string[] _GetConfigurationWarnings()
    {
        try{_ = new MobaHotseatSession(BetaSceneReader.Extract(this),StartingPlayer,developmentOpenTargeting:DevelopmentOpenTargeting,scoringProfile:DevelopmentScoring.Snapshot(),timerConfiguration:DevelopmentTimer.Snapshot(),assassinProfile:DevelopmentAssassin.Snapshot());return Array.Empty<string>();}
        catch(Exception e){return new[]{e.Message};}
    }
    private void ValidateAuthoring()
    {
        var errors=_GetConfigurationWarnings();UpdateConfigurationWarnings();
        if(errors.Length>0)GD.PushError(string.Join("\n",errors));
        else GD.Print($"AUTHORING VALID: {Name}; {BetaSceneReader.Extract(this).Obstacles.Count} blocked cells. No nodes changed.");
    }
    public Vector3 CellPosition(Cell cell,float height=0)
    {var p=Layout.Board.CellCenter(cell);return new((float)p.X,height,(float)p.Z);}
    private void BuildHud()
    {
        var canvas=new CanvasLayer();AddChild(canvas);
        var panel=new PanelContainer {Position=new(14,10)};canvas.AddChild(panel);
        var column=new VBoxContainer();panel.AddChild(column);
        column.AddChild(new Label {Text=$"PAWNS OF DIVINITY · ONE-LANE BETA · {Layout.Board.Width} × {Layout.Board.Height}"});
        column.AddChild(new Label {Text="WASD pan · Q/E / middle drag rotate · wheel zoom · Home recenter · G grid · click select · right click clear"});
        _turn=new Label();column.AddChild(_turn);
        var readinessRow=new HBoxContainer();column.AddChild(readinessRow);_readiness=new Label();readinessRow.AddChild(_readiness);BuildTimerHud(readinessRow);
        _status=new Label {Text="Hover a tile to inspect its coordinates and authored walkability."};column.AddChild(_status);
        var bottom=new PanelContainer {AnchorTop=1,AnchorBottom=1,AnchorRight=1,
            OffsetLeft=14,OffsetRight=-14,OffsetTop=-184,OffsetBottom=-10};canvas.AddChild(bottom);
        var info=new VBoxContainer();bottom.AddChild(info);
        _details=new Label();info.AddChild(_details);
        _endActivation=new Button {Name="EndActivation",Text="End Activation (skip remaining actions)",FocusMode=Control.FocusModeEnum.None};
        var buttons=new HBoxContainer();info.AddChild(buttons);
        _endActivation.Pressed+=()=>RunCommand(()=>Session.EndSelectedActivation());buttons.AddChild(_endActivation);
        _attack=new Button {Name="Attack",Text="Attack",FocusMode=Control.FocusModeEnum.None};_attack.Pressed+=ToggleAttackMode;buttons.AddChild(_attack);
        _confirmAttack=new Button {Name="ConfirmAttack",Text="Confirm Attack",FocusMode=Control.FocusModeEnum.None};_confirmAttack.Pressed+=ConfirmBasicAttack;buttons.AddChild(_confirmAttack);
        _resetMatch=new Button {Name="ResetMatch",Text="Reset Match (DEV)",FocusMode=Control.FocusModeEnum.None};_resetMatch.Pressed+=ResetMatch;buttons.AddChild(_resetMatch);
        _attackPreview=new Label();_attackPreview.AddThemeFontSizeOverride("font_size",14);info.AddChild(_attackPreview);
        _message=new Label {Text="Lime outlines are reachable movement; cyan/gold/purple floors are authored zones."};
        _message.AutowrapMode=TextServer.AutowrapMode.WordSmart;_message.AddThemeFontSizeOverride("font_size",14);info.AddChild(_message);
        _combatHud=new MobaCombatHud();AddChild(_combatHud);_combatHud.Initialize(_fixtures,_camera);
        BuildTerritoryHud(canvas,column,info,buttons);
        BuildAssassinHud(readinessRow,canvas);
    }
    private void RunCommand(Func<PawnsOfDivinity.Gameplay.BattlefieldCommandResult> command)
    {
        try{_message.Text=command().Reason;}
        catch(InvalidOperationException error){_message.Text=error.Message;GD.PushError(error.ToString());}
        RefreshMatchHud();
        UpdateMovementOverlay(null);
    }
    private void RefreshMatchHud()
    {
        RefreshTimerHud();
        var player=Session.CurrentPlayer;
        int blue=Session.Characters.Values.Count(c=>c.TeamId==1),red=Session.Characters.Count-blue;
        int slots=MobaHotseatSession.MaxRosterSlotsPerPlayer;
        _turn.Text=$"Round {Session.Round} · "+(player is { } p?
            (Session.Activations.ActiveCharacter is { } active?$"PLAYER {p} · ACTIVE {active.Name}":$"PLAYER {p} TO SELECT"):"No eligible living units")+
            $" · P1 {blue}/{slots} slots · P2 {red}/{slots} slots · "+
            (blue==slots&&red==slots?"5v5":$"{blue}v{red} development slice; competitive target 5v5");
        _readiness.Text=Session.Readiness(1).Text+"     "+Session.Readiness(2).Text;
        _turn.Modulate=player==1?new(.4f,.8f,1):player==2?new(1,.5f,.55f):Colors.Gray;
        if(Session.Victory.IsFinished)_turn.Text=$"Round {Session.Round} · MATCH FINISHED · "+(Session.Victory.WinnerTeamId is { } winner?$"PLAYER {winner} WINS":"DRAW (DEV tie policy)");
        if(Session.SelectedCharacter is { } c)
        {
            var state=Session.Capture(c.Id);var stats=c.BaseStats;
            string position=state.Position is { } pos?$"({pos.X}, {pos.Y})":"unplaced";
            _details.Text=$"SELECTED: {state.Name} · Player {state.TeamId} · HP {state.HP}/{state.MaxHP} · Shield {state.ShieldHP} · "+
                $"{(state.Defeated?"Defeated":state.HasActed?"Used this round":"Ready")}\n"+
                $"STR {stats.Strength} · DEF {c.EffectiveDefense} · INT {stats.Intelligence} · RES {c.EffectiveResistance} · WIS {stats.Wisdom} · SPD {stats.Speed} · MOV {state.Movement} · "+
                $"Position {position} · Movement available {Session.RemainingMovement}{(Session.IsPresentationBusy?" · TRAVELLING":"")}";
            if(state.Statuses.Count>0)_details.Text+=$" · {string.Join(", ",state.Statuses)}";
        }
        else _details.Text="No unit selected. Left click a friendly model or its tile; right click to deselect.\nLime outlines: reachable · White: path · Red: unavailable · Cyan/gold/purple floors are authored zones.";
        _endActivation.Disabled=Session.SelectedFixtureId is not { } id||!Session.CanSelect(id);
        foreach(var view in _fixtures)
        {
            var visibility=Session.Visibility(view.CharacterId);
            view.RefreshRuntime(view.CharacterId==Session.SelectedFixtureId,Session.CanSelect(view.CharacterId),Session.IsVisibleToPlayer(view.CharacterId),visibility.IsInvisible?"INVISIBLE":visibility.IsInSmoke?"IN SMOKE":"");
        }
        RefreshAttackHud();
        _resetMatch.Disabled=!Session.CanResetMatch;_combatHud.Refresh(Session);
        RefreshTerritoryHud();
        RefreshAssassinHud();
    }
    private void ToggleAttackMode()
    {
        CancelAbilityUi();
        _transferMode=false;
        if(_attackMode){_attackMode=false;_attackTargetId=null;_message.Text="Targeting cancelled; the chosen activation remains available.";}
        else
        {
            var result=Session.BeginAttackTargeting();_attackMode=result.Outcome==PawnsOfDivinity.Gameplay.BattlefieldOutcome.Completed;
            _attackTargetId=null;_message.Text=result.Reason;
        }
        RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void RefreshAttackHud()
    {
        if(_attackMode&&!Session.CanAttack){_attackMode=false;_attackTargetId=null;}
        _attack.Disabled=!Session.CanAttack;_attack.Text=_attackMode?"Cancel targeting":"Attack";
        foreach(var entry in _attackRings)
            entry.Value.Visible=_attackMode&&Session.PreviewAttack(entry.Key).CanExecute;
        var preview=_attackTargetId is { } id&&_attackMode?Session.PreviewAttack(id):null;
        _confirmAttack.Disabled=preview?.CanExecute!=true;
        _attackPreview.Text=preview?.CanExecute==true
            ?$"{Session.Characters[preview!.TargetId].Name}: distance {preview.Distance} / range {preview.MinimumRange}–{preview.MaximumRange} · {preview.HitChance}% hit · ESTIMATE {preview.EstimatedOpeningDamage} opening damage on hit; shields/counters/follow-ups apply."
            :_attackMode?"Select a red-ringed enemy. Preview does not roll or deal damage.":"Basic attacks: DEV loadouts / open targeting (LOS unresolved). A completed Main Action ends activation.";
    }
    private void ConfirmBasicAttack()
    {
        if(!_attackMode||_attackTargetId is not { } target||Session.IsPresentationBusy)return;
        var result=Session.ExecuteBasicAttack($"ui-basic-{++_attackSequence}",target);_message.Text=result.Reason;
        if(result.Combat is not null&&Session.CombatHistory.Records.LastOrDefault() is { } playback)
        {_combatClockToken=Session.HoldClockForCombatPresentation(playback.RequestId);_combatHud.Present(playback);}
        RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void ResetMatch()
    {
        if(!Session.CanResetMatch)return;
        var fresh=Session.CreateResetSession();
        Session=fresh;_pendingSelection=null;_attackMode=false;_attackTargetId=null;_attackSequence=0;_transferMode=false;_transferSequence=0;
        _combatHud.Clear();_combatClockToken=null;_seenTimeout=0;
        CancelAbilityUi();_abilityRequest=0;
        foreach(var view in _fixtures){view.BindRuntime(fresh.Characters[view.CharacterId]);view.ReconcileToRuntime(Layout.Board);}
        _hover.Visible=false;_status.Text="Match reset to the loaded authored layout.";
        _message.Text="DEV reset: fresh HP, shields, statuses, positions, round and activation state. Combat history cleared.";
        RefreshMatchHud();UpdateMovementOverlay(null);
    }
    private void SelectAt(Vector2 screen)
    {
        if(Session.IsPresentationBusy||Session.Victory.IsFinished)return;
        var from=_camera.ProjectRayOrigin(screen);var direction=_camera.ProjectRayNormal(screen);
        string? id=null;
        Cell? floor=null;
        var candidates=new List<(string Id,float Score)>();
        var query=PhysicsRayQueryParameters3D.Create(from,from+direction*200,2);
        var excluded=new Godot.Collections.Array<Rid>();
        // Crowded formations can project several picking boxes onto one point. Resolve the
        // nearest visible model anchor or occupied floor anchor rather than always the front box.
        for(int i=0;i<_fixtures.Count;i++)
        {
            query.Exclude=excluded;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count==0)break;
            if(hit["collider"].AsGodotObject() is not CollisionObject3D body)break;
            excluded.Add(body.GetRid());
            if(!body.HasMeta("fixture_id"))continue;
            string fixtureId=body.GetMeta("fixture_id").AsString();var view=_fixtures.Single(f=>f.CharacterId==fixtureId);
            candidates.Add((fixtureId,screen.DistanceSquaredTo(_camera.UnprojectPosition(view.GlobalPosition+new Vector3(0,.85f,0)))));
        }
        if(Pick(screen,out var cell))
        {
            floor=cell;
            var occupant=Session.Characters.Values.FirstOrDefault(c=>!c.IsDefeated&&c.TacticalPosition==cell);
            if(occupant is not null&&Session.IsVisibleToPlayer(occupant.Id))candidates.Add((occupant.Id,screen.DistanceSquaredTo(_camera.UnprojectPosition(CellPosition(cell)))));
        }
        if(candidates.Count>0)id=candidates.OrderBy(c=>c.Score).ThenBy(c=>c.Id,StringComparer.Ordinal).First().Id;
        if(_abilityMode)
        {
            if(Session.TargetedAbilityId==HoodedAssassin.SmokeBombId)_abilityDestination=floor??(Pick(screen,out var tile)?tile:null);
            else _abilityTarget=id;
            RefreshMatchHud();return;
        }
        if(_transferMode)
        {
            int index=_transferPlans.FindIndex(p=>p.DeploymentCell==floor);
            if(index>=0){_transferChoices.Select(index);_message.Text="Deployment tile selected; Confirm Transfer.";}
            else _message.Text="Choose a valid friendly destination deployment tile or use the dropdown.";
            RefreshMatchHud();return;
        }
        if(_attackMode)
        {
            var preview=id is null?null:Session.PreviewAttack(id);
            _attackTargetId=preview?.CanExecute==true?id:null;
            _message.Text=preview?.Reason??"Select a legal enemy target; movement is not submitted in attack mode.";
            RefreshMatchHud();return;
        }
        if(id is null&&floor is { } destination&&Session.SelectedCharacter is not null){MoveAt(destination);return;}
        RunCommand(()=>Session.Select(id));
    }
    private void MoveAt(Cell destination)
    {
        UpdateMovementOverlay(destination); // Re-read the same session query before using a displayed plan.
        var result=_hoverPlan is { } plan&&plan.Destination==destination?Session.CommitMovement(plan):Session.MoveTo(destination);
        _message.Text=$"{result.Outcome}: {result.Reason} · cost {result.TraversedCost}/{result.ApprovedCost?.ToString()??"—"}.";
        if(Session.PendingVisualMovement is { } move)
        {
            var view=_fixtures.Single(f=>f.CharacterId==move.ActorId);
            try{view.PresentMovement(move,Layout.Board);}
            catch(Exception error)
            {view.ReconcileToRuntime(Layout.Board);Session.AcknowledgeVisualMovement(move.RequestId,view.Cell);GD.PushWarning($"View reconciled after playback failure: {error.Message}");}
        }
        RefreshMatchHud();
        UpdateMovementOverlay(null);
    }
    private static StandardMaterial3D Overlay(Color color)=>new(){AlbedoColor=color,Transparency=BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
    private void UpdateMovementOverlay(Cell? hover)
    {
        _status.Text=hover is { } inspected?DescribeTile(inspected):Session.Victory.IsFinished?"Match finished; Reset Match starts a fresh game.":"Hover a tile to inspect its coordinates and runtime control.";
        _movementPreview=MobaMovementPreview.Capture(Session);
        _reachable=_movementPreview.Routes;
        {
            foreach(var tile in _movementTiles.Values)tile.Visible=false;
            foreach(var cell in _reachable.Keys)
            {
                if(!_movementTiles.TryGetValue(cell,out var tile))
                {
                    var edge=new SurfaceTool();edge.Begin(Mesh.PrimitiveType.Lines);
                    float half=(float)Layout.Board.CellSize*.43f;
                    Vector3[] corners={new(-half,0,-half),new(half,0,-half),new(half,0,half),new(-half,0,half)};
                    for(int i=0;i<4;i++){edge.AddVertex(corners[i]);edge.AddVertex(corners[(i+1)%4]);}
                    tile=new MeshInstance3D {Name=$"Reachable_{cell.X}_{cell.Y}",Position=CellPosition(cell,.035f),Mesh=edge.Commit()};
                    AddChild(tile);_movementTiles.Add(cell,tile);
                }
                tile.Visible=true;
            }
        }
        _hoverPlan=!_attackMode&&!_transferMode&&!_abilityMode&&hover is { } destination?_movementPreview.PlanFor(destination):null;
        var path=_hoverPlan?.Path.ToHashSet();
        foreach(var entry in _movementTiles.Where(e=>e.Value.Visible))
            entry.Value.MaterialOverride=path?.Contains(entry.Key)==true?_pathMaterial:_reachableMaterial;
        _hover.MaterialOverride=_neutralHoverMaterial;
        if(_attackMode||_transferMode||_abilityMode){_hover.Visible=false;if(_abilityMode)foreach(var tile in _movementTiles.Values)tile.Visible=false;return;}
        if(hover is { } h&&Session.SelectedCharacter is not null)
        {
            _hover.MaterialOverride=_reachable.ContainsKey(h)?_pathMaterial:_invalidMaterial;
            _status.Text+=$" · {(_hoverPlan is { } p?$"Shortest path: {p.Cost}/{_movementPreview.Allowance} MOV · {p.Path.Count-1} steps":"No available legal movement to this tile")}";
        }
    }
    private string DescribeTile(Cell cell)
    {
        var castle=Layout.Castles.FirstOrDefault(c=>c.Footprint.Contains(cell));
        var zone=castle is not null?Session.Control.Castle(castle.Id):Layout.Objective.Contains(cell)?Session.Control.Objective:null;
        string control=zone is null?"neutral field":$"{zone.Id} · {(zone.OwnerTeamId is { } p?$"Player {p} controlled":"neutral")} {(zone.Contested?"· CONTESTED":"")}";
        return $"Tile ({cell.X}, {cell.Y}) · {(Layout.Walkable(cell)?"walkable":"static obstacle")} · {control}";
    }
    private void AdvanceMovementViews(double delta)
    {
        foreach(var view in _fixtures)view.AdvanceMovement(delta);
        if(Session.PendingVisualMovement is { } move)
        {
            var view=_fixtures.Single(f=>f.CharacterId==move.ActorId);
            if(!view.Moving)
            {
                view.ReconcileToRuntime(Layout.Board);
                var result=Session.AcknowledgeVisualMovement(move.RequestId,view.Cell);
                _message.Text=result.Reason;
            }
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        if(Engine.IsEditorHint()||_pendingSelection is not { } screen)return;
        _pendingSelection=null;if(!Session.IsPresentationBusy)SelectAt(screen);
    }

    // Adapted directly from Battlefield's existing free tactical camera controls/equations.
    // Kept local because that script also owns the production 14x14 encounter and is dirty.
    private void ResetCamera()
    { _focus=new((float)Layout.Board.CenterX,0,(float)Layout.Board.CenterZ);_yaw=0;_camera.Size=(float)(Layout.Board.Width*Layout.Board.CellSize*1.16);UpdateCamera(); }
    private void UpdateCamera()
    { _camera.Position=_focus+new Vector3(0,24,32).Rotated(Vector3.Up,_yaw);_camera.LookAt(_focus,Vector3.Up); }
    public bool Pick(Vector2 screen,out Cell cell)
    {
        var origin=_camera.ProjectRayOrigin(screen);var direction=_camera.ProjectRayNormal(screen);
        cell=default;if(Mathf.Abs(direction.Y)<.00001f) return false;
        float t=-origin.Y/direction.Y;if(t<0)return false;
        var point=origin+direction*t;cell=Layout.Board.WorldToCell(point.X,point.Z);return Layout.Board.Contains(cell);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(Engine.IsEditorHint() || _camera is null)return;
        if(input is InputEventKey key && key.Pressed && !key.Echo)
        {
            var code=key.PhysicalKeycode!=Key.None ? key.PhysicalKeycode : key.Keycode;
            if(code==Key.G)_grid.Visible=!_grid.Visible;if(code==Key.Home)ResetCamera();
        }
        if(input is InputEventMouseMotion motion && (motion.ButtonMask&MouseButtonMask.Middle)!=0)
        {_yaw-=motion.Relative.X*.008f;UpdateCamera();}
        if(input is not InputEventMouseButton mouse || !mouse.Pressed)return;
        if(mouse.ButtonIndex==MouseButton.WheelUp)_camera.Size=Mathf.Max(12,_camera.Size-1.5f);
        if(mouse.ButtonIndex==MouseButton.WheelDown)_camera.Size=Mathf.Min((float)(Layout.Board.Width*Layout.Board.CellSize*1.8),_camera.Size+1.5f);
        if(Session.IsPresentationBusy)return;
        if(mouse.ButtonIndex==MouseButton.Right){_pendingSelection=null;_attackMode=false;_attackTargetId=null;_transferMode=false;CancelAbilityUi();RunCommand(()=>Session.Select(null));}
        if(mouse.ButtonIndex==MouseButton.Left)_pendingSelection=mouse.Position;
    }
    private void AdvanceCamera(double delta,float x,float z,float turn)
    {
        var next=_focus+new Vector3(x,0,z).Rotated(Vector3.Up,_yaw).Normalized()*(float)delta*8;
        var min=CellPosition(new(0,0));var max=CellPosition(new(Layout.Board.Width-1,Layout.Board.Height-1));
        _focus=new(Mathf.Clamp(next.X,min.X,max.X),0,Mathf.Clamp(next.Z,min.Z,max.Z));
        _yaw+=turn*(float)delta;
        UpdateCamera();
    }
    public override void _Process(double delta)
    {
        if(Engine.IsEditorHint() || _camera is null)return;
        float x=(Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0);
        float z=(Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0);
        float turn=(Input.IsPhysicalKeyPressed(Key.Q)?1:0)-(Input.IsPhysicalKeyPressed(Key.E)?1:0);
        AdvanceCamera(delta,x,z,turn);
        AdvanceMovementViews(delta);
        AdvanceActivationClock();
        RefreshMatchHud();
        Cell? hovered=null;
        if(Pick(GetViewport().GetMousePosition(),out var cell))
        { hovered=cell;_hover.Visible=true;_hover.Position=CellPosition(cell,.025f);_status.Text=$"Tile ({cell.X}, {cell.Y}) · {(Layout.Walkable(cell)?"walkable":"static obstacle")} · "+
            (Layout.Castles.FirstOrDefault(c=>c.Footprint.Contains(cell))?.Id ?? "neutral field"); }
        else _hover.Visible=false;
        if(_captureMovement&&_captureStep==0)
        {hovered=new(23,10);_hover.Visible=true;_hover.Position=CellPosition(hovered.Value,.025f);_status.Text="Preview destination (23,10) · Red castle's open eastern gate";}
        UpdateMovementOverlay(hovered);
        _elapsed+=delta;
        if(_smoke && _elapsed>1) {SmokeTest();return;}
        if(_capture && _elapsed>(_captureFeedback?(_captureStep==0?2.1:1.1):_captureMovement&&_captureStep==1?.4:2))
        {
            _elapsed=0;
            if(_captureAssassin)
            {
                string[] names={"preview","smoke","shadow_preview","shadow_result","reset"};string file=$"res://artifacts/moba_assassin_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"ASSASSIN CAPTURE (runtime-only staging): {file}");
                if(_captureStep++==0)_confirmAbility.EmitSignal(Button.SignalName.Pressed);
                else if(_captureStep==2)StageInvisibleShadowPreview();
                else if(_captureStep==3)_confirmAbility.EmitSignal(Button.SignalName.Pressed);
                else if(_captureStep==4){ResetMatch();ResetCamera();}
                else GetTree().Quit();
                return;
            }
            if(_captureTimer)
            {
                string[] names={"initial","urgent","timeout"};string file=$"res://artifacts/moba_timer_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"TIMER CAPTURE (injected DEV clock): {file}");
                if(_captureStep++==0)_captureTime!.NowSeconds+=Session.ActivationTimer.Configuration.DurationSeconds*.9;
                else if(_captureStep==2)_captureTime!.NowSeconds+=Session.ActivationTimer.Configuration.DurationSeconds*.1+.01;
                else GetTree().Quit();
                return;
            }
            if(_captureObjectives)
            {
                string[] names={"contested","capture","victory","reset"};string file=$"res://artifacts/moba_objectives_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"OBJECTIVE CAPTURE (temporary runtime setup): {file}");
                if(_captureStep++==0)CompleteContestedCaptureDemonstration();else if(_captureStep==2)StageObjectiveVictoryDemonstration();else if(_captureStep==3)ResetMatch();else GetTree().Quit();
                return;
            }
            if(_captureFive)
            {
                string[] names={"overhead","blue_castle","red_castle"};string file=$"res://artifacts/moba_5v5_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"5V5 CAPTURE: {file}");
                if(_captureStep++==0){_focus=CellPosition(new(8,12));_camera.Size=14;UpdateCamera();RunCommand(()=>Session.Select("blue_time_weaver"));}
                else if(_captureStep==2){_endActivation.EmitSignal(Button.SignalName.Pressed);_focus=CellPosition(new(21,12));UpdateCamera();RunCommand(()=>Session.Select("red_gentle_guardian"));}
                else GetTree().Quit();
                return;
            }
            if(_captureFeedback)
            {
                string[] names={"combat","defeat","reset"};string file=$"res://artifacts/moba_feedback_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"FEEDBACK CAPTURE (temporary runtime setup): {file}");
                if(_captureStep++==0)
                {
                    int round=Session.Round;
                    while(Session.Round==round){Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id);Session.EndSelectedActivation();}
                    _combatHud.Clear();StageAttackDemonstration();var target=Session.Characters["nigel"];target.SetShieldHP(0);target.TakeDamage(target.CurrentHealth-1);
                    ConfirmBasicAttack();_combatHud.ToggleHistory();
                }
                else if(_captureStep==2){ResetMatch();ResetCamera();}
                else GetTree().Quit();
                return;
            }
            if(_captureAttack)
            {
                string file=$"res://artifacts/moba_attack_{(_captureStep==0?"preview":"result")}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"ATTACK CAPTURE (temporary runtime positions only): {file}");
                if(_captureStep++==0)ConfirmBasicAttack();else GetTree().Quit();
                return;
            }
            if(_captureMovement)
            {
                string[] names={"preview","travelling","complete"};
                string file=$"res://artifacts/moba_movement_{names[_captureStep]}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);GD.Print($"MOVEMENT CAPTURE: {file}");
                if(_captureStep++==0)MoveAt(new(23,10));
                else if(_captureStep==3)GetTree().Quit();
                return;
            }
            var path=_captureHotseat ? (_captureStep==0 ? "res://artifacts/moba_hotseat_selection.png" : "res://artifacts/moba_hotseat_player2.png") :
                (_captureStep==0 ? "res://artifacts/moba_beta_overhead.png" : "res://artifacts/moba_beta_castle.png");
            GetViewport().GetTexture().GetImage().SavePng(path);GD.Print($"BETA CAPTURE: {path}");
            if(_captureStep++==0)
            {
                if(_captureHotseat)
                {
                    _endActivation.EmitSignal(Button.SignalName.Pressed);
                    RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));
                }
                else {_focus=CellPosition(new(9,11));_camera.Size=15;UpdateCamera();}
            }
            else GetTree().Quit();
        }
    }
    // Explicit developer smoke/capture setup; never writes fixtures or scene-authored spawn data.
    private void StageAttackDemonstration()
    {
        while(Session.CurrentPlayer!=1)
        {Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id);Session.EndSelectedActivation();}
        Session.Characters["hooded_assassin"].SetTacticalPosition(new(14,10));
        Session.Characters["nigel"].SetTacticalPosition(new(15,10));
        foreach(var id in new[]{"hooded_assassin","nigel"})_fixtures.Single(f=>f.CharacterId==id).ReconcileToRuntime(Layout.Board);
        RunCommand(()=>Session.Select("hooded_assassin"));ToggleAttackMode();_attackTargetId="nigel";RefreshMatchHud();
    }
    private void SmokeTest()
    {
        try
        {
            _smoke=false;
            void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
            Check(_fixtures.Count==Layout.Fixtures.Count,"Fixture count");Check(Layout.Terrain.Count==Layout.Board.Width*Layout.Board.Height,"Floor count");
            BetaSceneRoundTrip.Verify(SceneFilePath);
            // Temporary PackedScenes retain native resource wrappers until managed GC runs.
            // Release them before shutting down the development smoke-test process.
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            foreach(var yaw in new[]{0f,.8f,-1.3f})
            {
                _yaw=yaw;UpdateCamera();
                foreach(var cell in Layout.Board.Cells)
                    Check(Pick(_camera.UnprojectPosition(CellPosition(cell)),out var picked)&&picked==cell,$"Picking {cell} at yaw {yaw}");
            }
            Check(_fixtures.Count(f=>!f.UsedFallback)==Layout.Fixtures.Count,"Expected all original imported models");
            foreach(var f in _fixtures) {f.Select(true);f.Select(false);}
            ResetCamera();
            Check(_endActivation.Disabled,"End Activation must require selection");
            var friendly=_fixtures.Single(f=>f.CharacterId==Session.Characters.Values.First(c=>c.TeamId==Session.CurrentPlayer).Id);
            var torso=_camera.UnprojectPosition(friendly.GlobalPosition+new Vector3(0,.8f,0));
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.Left,Pressed=true,Position=torso});
            _PhysicsProcess(0);
            Check(Session.SelectedFixtureId==friendly.CharacterId&&!_endActivation.Disabled,"Model click must select runtime fixture");
            Check(_details.Text.Contains($"HP {friendly.RuntimeCharacter!.CurrentHealth}/{friendly.RuntimeCharacter.BaseStats.MaxHealth}"),"Runtime HP HUD");
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.Right,Pressed=true});
            Check(Session.SelectedFixtureId is null&&_endActivation.Disabled,"Right-click deselection");
            int initialRound=Session.Round;var completed=new HashSet<string>();
            while(Session.Round==initialRound)
            {
                var actor=Session.Characters.Values.First(c=>Session.CanSelect(c.Id));
                int player=actor.TeamId;
                var actorView=_fixtures.Single(f=>f.CharacterId==actor.Id);
                SelectAt(_camera.UnprojectPosition(actorView.GlobalPosition+new Vector3(0,.85f,0)));
                Check(Session.SelectedFixtureId==actor.Id,$"Crowded model selection: {actor.Id}");
                RunCommand(()=>Session.Select(null));
                SelectAt(_camera.UnprojectPosition(CellPosition(actor.TacticalPosition!.Value)));
                Check(Session.SelectedFixtureId==actor.Id,"Tile selection");
                // Emit the real button signal, following the exact UI-to-domain path.
                _endActivation.EmitSignal(Button.SignalName.Pressed);
                Check(completed.Add(actor.Id),"Unit activated twice");
                if(Session.Round==initialRound)Check(actor.HasActedThisRound,"Domain activation flag");
                if(Session.Round==initialRound&&Session.Characters.Values.Any(c=>c.TeamId!=player&&c.CanAct))
                    Check(Session.CurrentPlayer!=player,"Players must alternate when both are eligible");
            }
            Check(completed.Count==Session.Characters.Count&&Session.Round==initialRound+1,"Automatic round reset");
            Check(Session.Characters.Values.All(c=>!c.HasActedThisRound),"Readiness reset");
            var nigel=_fixtures.Single(f=>f.CharacterId=="nigel");
            var authoredNigelCell=Layout.Fixtures.Single(f=>f.CharacterId=="nigel").Cell;
            // Historical movement smoke starts outside the fort; the authored 5v5 spawn is retained.
            Session.Characters["nigel"].SetTacticalPosition(new(27,10));nigel.ReconcileToRuntime(Layout.Board);
            if(Session.CurrentPlayer!=Session.Characters["nigel"].TeamId)
            {
                RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));
                _endActivation.EmitSignal(Button.SignalName.Pressed);
            }
            // Staged transforms have not reached a physics tick; use the authoritative occupied tile.
            SelectAt(_camera.UnprojectPosition(nigel.Position));
            Check(Session.SelectedFixtureId=="nigel"&&Session.RemainingMovement==4,"Nigel MOV 4 selection");
            UpdateMovementOverlay(new(26,9));
            Check(_hoverPlan is null&&_hover.MaterialOverride==_invalidMaterial,"Blocked destination presentation");
            Check(Session.MoveTo(new(26,9)).Outcome==MovementOutcome.Rejected&&Session.Activations.ActiveCharacter is null,"Invalid move consumes nothing");
            UpdateMovementOverlay(new(25,10));
            Check(_hoverPlan?.Cost==2&&_movementTiles[new(25,10)].Visible,"Reachable highlight and shortest-path preview");
            Check(_status.Text.Contains("2/4 MOV"),"Actual path cost / movement budget label");
            RunCommand(()=>Session.Select(null));
            Check(_hoverPlan is null&&_movementTiles.Values.All(v=>!v.Visible)&&_hover.MaterialOverride==_neutralHoverMaterial,"Deselection clears cached path/highlights immediately");
            RunCommand(()=>Session.Select("nigel"));UpdateMovementOverlay(new(25,10));
            Check(_movementTiles.Where(e=>e.Value.Visible).Select(e=>e.Key).ToHashSet().SetEquals(Session.ReachableDestinations().Keys),"Visible movement destinations equal authoritative routes");
            Check(_reachableMaterial.AlbedoColor!=_pathMaterial.AlbedoColor,"Reachable and path colors differ");
            var visualStart=nigel.Position;
            SelectAt(_camera.UnprojectPosition(CellPosition(new(25,10))));
            Check(Session.Characters["nigel"].TacticalPosition==new Cell(25,10),"Production position commit");
            Check(Session.IsPresentationBusy&&nigel.Moving&&_endActivation.Disabled,"Animation input lock");
            Check(nigel.Position==visualStart,"Visual starts at actual traversed origin");
            Check(Session.MoveTo(new(24,10)).Outcome==MovementOutcome.Rejected,"Duplicate movement blocked");
            _endActivation.EmitSignal(Button.SignalName.Pressed);
            Check(!Session.Characters["nigel"].HasActedThisRound,"End blocked during animation");
            AdvanceMovementViews(.1);
            Check(nigel.Position.DistanceTo(visualStart)>0&&nigel.Position.DistanceTo(visualStart)<1,"Smooth intermediate position");
            AdvanceMovementViews(20);RefreshMatchHud();
            Check(!Session.IsPresentationBusy&&!nigel.Moving&&nigel.Position==CellPosition(new(25,10)),"Visual/domain reconciliation");
            Check(Session.Activations.CanUseMainAction&&!Session.Characters["nigel"].HasActedThisRound,"Move retains activation/Main Action");
            Check(_details.Text.Contains("Position (25, 10)"),"Runtime coordinate HUD");
            Check(Session.RemainingMovement==0&&Session.MoveTo(new(24,10)).Outcome==MovementOutcome.Rejected,"Unsplit movement opportunity");
            Check(Layout.Fixtures.Single(f=>f.CharacterId=="nigel").Cell==authoredNigelCell,"Authored spawn remains unchanged");
            _endActivation.EmitSignal(Button.SignalName.Pressed);
            Check(Session.CurrentPlayer==1,"End after movement passes control");
            int movementRound=Session.Round;
            while(Session.Round==movementRound)
            {
                RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));
                _endActivation.EmitSignal(Button.SignalName.Pressed);
            }
            Check(!Session.Activations.MovementUsed&&Session.Characters["nigel"].TacticalPosition==new Cell(25,10),"Round reset preserves moved positions");
            StageAttackDemonstration();
            Check(_attackMode&&!_confirmAttack.Disabled&&_attackRings["nigel"].Visible,"Attack button enables legal target and confirmation");
            Check(!_attackRings["gentle_guardian"].Visible&&_attackPreview.Text.Contains("100% hit"),"No allied targets; production chance preview");
            int hpBefore=Session.Characters["nigel"].CurrentHealth;
            // Exercise the real target-click handler using the target's occupied floor cell.
            SelectAt(_camera.UnprojectPosition(CellPosition(new(15,10))));
            Check(_attackTargetId=="nigel"&&Session.Characters["nigel"].CurrentHealth==hpBefore,"Target click previews without damage");
            _confirmAttack.EmitSignal(Button.SignalName.Pressed);
            Check(Session.Characters["nigel"].CurrentHealth<hpBefore&&_message.Text.Contains("HIT"),"Production attack updates actual HP and feedback");
            Check(Session.Characters["hooded_assassin"].HasActedThisRound&&Session.CurrentPlayer==2&&!_attackMode&&_confirmAttack.Disabled,"Production Main Action ends activation and passes control");
            Check(_attackRings.Values.All(r=>!r.Visible)&&_movementTiles.Values.All(t=>!t.Visible),"Attack completion clears targeting and movement overlays");
            hpBefore=Session.Characters["nigel"].CurrentHealth;_confirmAttack.EmitSignal(Button.SignalName.Pressed);
            Check(Session.Characters["nigel"].CurrentHealth==hpBefore,"Repeated confirmation does nothing");
            Check(Layout.Fixtures.Single(f=>f.CharacterId=="nigel").Cell==authoredNigelCell,"Attack test leaves authored source unchanged");
            Check(Session.CombatHistory.Records.Count==1&&_combatHud.FloatingCount>0&&_combatHud.HistoryText.Contains("Counter"),"Resolved combat populates chronological history and floating feedback");
            _combatHud.ToggleHistory();Check(_combatHud.HistoryExpanded,"Combat history expands");_combatHud.ToggleHistory();Check(!_combatHud.HistoryExpanded,"Combat history collapses");
            Check(_combatHud.HealthText("nigel").Contains($"{Session.Characters["nigel"].CurrentHealth}/52 HP"),"World health display reflects authoritative combat HP");
            for(int cycle=0;cycle<2;cycle++)
            {
                _resetMatch.EmitSignal(Button.SignalName.Pressed);
                Check(Session.Round==1&&Session.CurrentPlayer==StartingPlayer&&Session.SelectedCharacter is null,"Reset restores initial round/player/selection");
                Check(Session.CombatHistory.Records.Count==0&&_combatHud.HistoryText==""&&_combatHud.FloatingCount==0&&!_combatHud.HistoryExpanded,"Reset clears history and queued floating feedback");
                Check(_readiness.Text.Contains(Session.Readiness(1).Text)&&_readiness.Text.Contains(Session.Readiness(2).Text),"Reset readiness HUD reads both runtime rosters");
                foreach(var fixture in _fixtures)
                {
                    var c=Session.Characters[fixture.CharacterId];
                    Check(ReferenceEquals(c,fixture.RuntimeCharacter)&&c.TacticalPosition==fixture.Fixture.Cell&&fixture.GlobalPosition==CellPosition(fixture.Fixture.Cell),"Reset rebinds initial domain positions and visual transforms");
                    Check(c.CurrentHealth==c.BaseStats.MaxHealth&&c.ShieldHP==0&&!c.IsDefeated&&!c.HasActedThisRound,"Reset restores HP/shields/life/readiness");
                    Check(fixture.GetChildren().OfType<StaticBody3D>().Count()==1&&_combatHud.HealthVisible(c.Id),"Repeated reset reuses picking body and restores health bar");
                }
                StageAttackDemonstration();var target=Session.Characters["nigel"];target.SetShieldHP(0);target.TakeDamage(target.CurrentHealth-1);
                _confirmAttack.EmitSignal(Button.SignalName.Pressed);
                Check(target.IsDefeated&&!Session.CanSelect(target.Id)&&!_combatHud.HealthVisible(target.Id),"Lethal attack hides health and rejects active selection");
                Check(_readiness.Text.Contains(Session.Readiness(2).Text)&&Session.Readiness(2).Ready==Session.Readiness(2).Total-1,"Defeated units are excluded from the readiness HUD");
                Check(nigel.GetNode<StaticBody3D>("RuntimeSelectionBody").CollisionLayer==0&&nigel.GetNode<Label3D>("Nameplate").Text.Contains("DEFEATED"),"Defeated visual has no picking body and is labeled");
                Check(nigel.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Any(g=>g.MaterialOverride is StandardMaterial3D m&&m.AlbedoColor==Colors.DimGray),"Defeated original model has runtime grey material");
                _resetMatch.EmitSignal(Button.SignalName.Pressed);
                RunCommand(()=>Session.Select(Session.Characters.Values.First(c=>Session.CanSelect(c.Id)).Id));
                var route=Session.ReachableDestinations().First().Key;MoveAt(route);
                Check(Session.IsPresentationBusy,"Reset test stages pending visual movement");
                var cameraBeforeReset=(_focus,_yaw,_camera.Size);
                _pendingSelection=new Vector2(1,1);_resetMatch.EmitSignal(Button.SignalName.Pressed);
                Check(cameraBeforeReset==(_focus,_yaw,_camera.Size),"Reset preserves camera focus/orientation/zoom");
                Check(!Session.IsPresentationBusy&&_fixtures.All(f=>!f.Moving)&&_pendingSelection is null,"Reset cancels pending movement and old queued clicks");
                Check(_fixtures.All(f=>f.GetNode<StaticBody3D>("RuntimeSelectionBody").CollisionLayer==2),"Reset restores living picking bodies after defeat");
            }
            // Every fixture, including reused opposing definitions, must drive its own model's playback.
            var travelled=new HashSet<string>();int travelRound=Session.Round;
            while(Session.Round==travelRound)
            {
                var actor=Session.Characters.Values.First(c=>Session.CanSelect(c.Id));RunCommand(()=>Session.Select(actor.Id));
                var view=_fixtures.Single(f=>f.CharacterId==actor.Id);var destination=Session.ReachableDestinations().OrderBy(r=>r.Value.Cost).First().Key;
                MoveAt(destination);Check(view.Moving&&Session.PendingVisualMovement?.ActorId==actor.Id,$"Independent visual movement binding: {actor.Id}");
                AdvanceMovementViews(20);RefreshMatchHud();Check(view.Cell==destination&&view.GlobalPosition==CellPosition(destination)&&!view.Moving,$"Independent visual reconciliation: {actor.Id}");
                Check(travelled.Add(actor.Id),"No duplicate movement activations");_endActivation.EmitSignal(Button.SignalName.Pressed);
            }
            Check(travelled.Count==Session.Characters.Count,"All ten fixtures complete movement/alternating activations");
            _resetMatch.EmitSignal(Button.SignalName.Pressed);
            VerifyObjectiveSmoke();
            VerifyTimerSmoke();
            VerifyAssassinSmoke();
            float size=_camera.Size;
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.WheelUp,Pressed=true});
            Check(_camera.Size<size,"Wheel zoom in");
            _UnhandledInput(new InputEventMouseButton {ButtonIndex=MouseButton.WheelDown,Pressed=true});
            Check(Mathf.IsEqualApprox(size,_camera.Size),"Wheel zoom out");
            float yawBefore=_yaw;
            _UnhandledInput(new InputEventMouseMotion {ButtonMask=MouseButtonMask.Middle,Relative=new(40,0)});
            Check(!Mathf.IsEqualApprox(yawBefore,_yaw),"Middle-drag rotation");
            bool gridBefore=_grid.Visible;
            _UnhandledInput(new InputEventKey {PhysicalKeycode=Key.G,Pressed=true});
            Check(_grid.Visible!=gridBefore,"Grid toggle");
            _UnhandledInput(new InputEventKey {PhysicalKeycode=Key.G,Pressed=true});
            var focusBefore=_focus;
            AdvanceCamera(.5,1,0,0);
            Check(_focus.DistanceTo(focusBefore)>1,"Camera pan");
            _UnhandledInput(new InputEventKey {PhysicalKeycode=Key.Home,Pressed=true});
            Check(_focus==new Vector3((float)Layout.Board.CenterX,0,(float)Layout.Board.CenterZ) && _yaw==0,"Home recenter");
            if(_snapshotPath is not null)System.IO.File.WriteAllText(_snapshotPath,
                System.Text.Json.JsonSerializer.Serialize(Layout.Source));
            ResetCamera();GD.Print($"GODOT MOBA BETA SMOKE PASSED: authored round-trip, {_fixtures.Count} original fixtures, movement/combat/castles/VP/victory/timer regressions, Assassin ability UI, smoke preview/cast/teleport/cleanup, observer visibility and occupancy, pre-approach Shadow Strike/Disarm/reveal, ability timeout/reset and preserved camera/grid.");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
