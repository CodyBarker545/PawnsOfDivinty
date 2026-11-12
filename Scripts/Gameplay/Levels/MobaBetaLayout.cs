using System;
using System.Collections.Generic;
using System.Linq;
using PawnsOfDivinity.Core.Abilities;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Presentation geometry only; deliberately independent of the 14x14 combat adapter.</summary>
public sealed record RectangularBoard(int Width, int Height, double CellSize, double CenterX, double CenterZ)
{
    public bool Contains(GridPosition p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;
    public IEnumerable<GridPosition> Cells => MobaBetaLayout.Rectangle(0, Width - 1, 0, Height - 1);
    public (double X, double Z) CellCenter(GridPosition p) =>
        (CenterX + (p.X - (Width - 1) / 2.0) * CellSize,
         CenterZ + (p.Y - (Height - 1) / 2.0) * CellSize);
    public GridPosition WorldToCell(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(x));
        return new(checked((int)Math.Floor((x - CenterX) / CellSize + Width / 2.0)),
            checked((int)Math.Floor((z - CenterZ) / CellSize + Height / 2.0)));
    }
    public GridPosition Mirror(GridPosition p) => new(Width - 1 - p.X, p.Y);
}

public sealed record BetaCastle(string Id, int InitialTeamId, bool IsHome,
    IReadOnlySet<GridPosition> Footprint, IReadOnlySet<GridPosition> CaptureCells,
    IReadOnlySet<GridPosition> DeploymentCells, IReadOnlySet<GridPosition> Walls,
    IReadOnlyDictionary<string, IReadOnlySet<GridPosition>> Entrances);
public sealed record BetaCamp(string Id, IReadOnlySet<GridPosition> Cells);
public sealed record BetaCharacterFixture(string CharacterId, string DisplayName, int TeamId,
    GridPosition Cell, string? ModelPath, string DefinitionId = "");

/// <summary>Read-only runtime projection of scene-authored data. Contains no default map or ownership state.</summary>
public sealed class MobaBetaLayout
{
    public RectangularBoard Board { get; }
    public IReadOnlyList<BetaCastle> Castles { get; }
    public IReadOnlyList<BetaCamp> Camps { get; }
    public IReadOnlyList<BetaCharacterFixture> Fixtures { get; }
    public IReadOnlySet<GridPosition> Objective { get; }
    public IReadOnlySet<GridPosition> MainLane { get; }
    public IReadOnlySet<GridPosition> SouthernFlank { get; }
    public IReadOnlySet<GridPosition> Jungle { get; }
    public IReadOnlySet<GridPosition> NatureObstacles { get; }
    public IReadOnlySet<GridPosition> Obstacles { get; }
    public IReadOnlyDictionary<string, IReadOnlySet<GridPosition>> Connections { get; }
    public IReadOnlyDictionary<GridPosition, string> Terrain { get; }
    public AuthoredLevelData Source { get; }

    public MobaBetaLayout(AuthoredLevelData source)
    {
        Source=source;Board=source.Board;
        var errors=new List<string>();
        if(Board.Width<=0||Board.Height<=0||Board.Width>256||Board.Height>256||!double.IsFinite(Board.CellSize)||Board.CellSize<=0||
            !double.IsFinite(Board.CenterX)||!double.IsFinite(Board.CenterZ))
            throw new LevelAuthoringException(new[]{"BoardDefinition: dimensions must be 1–256; cell size positive; center/scale finite."});
        var rows=new List<(AuthoredElementData Data,HashSet<GridPosition> Cells)>();
        var ids=new HashSet<string>();
        var kinds=new[]{"Terrain","Castle","CaptureZone","DeploymentZone","Camp","Objective","Obstacle","Entrance","Route","Connection","Fixture"};
        foreach(var e in source.Elements)
        {
            string prefix=$"{e.Path} [{e.Id}]: ";
            if(string.IsNullOrWhiteSpace(e.Id)||!ids.Add(e.Id)) errors.Add(prefix+"Element ID must be nonempty and globally unique.");
            if(!kinds.Contains(e.Kind))errors.Add(prefix+$"Unknown element kind '{e.Kind}'.");
            if(!e.HasIdentityBasis)errors.Add(prefix+"Metadata roots require identity rotation and scale. Rotate/scale visual children instead.");
            if(!double.IsFinite(e.X)||!double.IsFinite(e.Y)||!double.IsFinite(e.Z)) {errors.Add(prefix+"Transform must be finite.");continue;}
            var anchor=Board.WorldToCell(e.X,e.Z);var center=Board.CellCenter(anchor);
            if(Math.Abs(e.X-center.X)>.001||Math.Abs(e.Z-center.Z)>.001||Math.Abs(e.Y)>.001)
                errors.Add(prefix+$"Not grid aligned. Snap X/Z to cell centers at {Board.CellSize} spacing and set Y=0. Nearest cell ({anchor.X},{anchor.Y}) center ({center.X},0,{center.Z}).");
            if(e.CellOffsets.Length==0||e.CellOffsets.Distinct().Count()!=e.CellOffsets.Length)errors.Add(prefix+"CellOffsets must be nonempty and contain no duplicates.");
            var cells=e.CellOffsets.Select(p=>new GridPosition(anchor.X+p.X,anchor.Y+p.Y)).ToHashSet();
            if(cells.Any(p=>!Board.Contains(p)))errors.Add(prefix+"Footprint is outside board bounds.");
            if(e.Kind=="Fixture"&&cells.Count!=1)errors.Add(prefix+"A character spawn anchor must occupy exactly one cell.");
            rows.Add((e,cells));
        }
        void Fail(){if(errors.Count>0)throw new LevelAuthoringException(errors);}
        Fail();
        var floors=rows.Where(r=>r.Data.Kind=="Terrain").ToArray();
        foreach(var r in floors)if(r.Cells.Count!=1||string.IsNullOrWhiteSpace(r.Data.TerrainTag))errors.Add($"{r.Data.Path}: Terrain needs exactly one cell and a TerrainTag.");
        var floorCells=floors.SelectMany(r=>r.Cells).ToArray();
        if(floorCells.Distinct().Count()!=floorCells.Length)errors.Add("Terrain: duplicate floor cells; move the terrain tile back or remove the duplicate.");
        if(!floorCells.ToHashSet().SetEquals(Board.Cells))errors.Add("Terrain: every board cell needs exactly one authored terrain tile (no gaps or out-of-bounds tiles).");
        var castleIds=rows.Where(r=>r.Data.Kind=="Castle").Select(r=>r.Data.Id).ToHashSet();
        foreach(var r in rows.Where(r=>r.Data.Kind is "CaptureZone" or "DeploymentZone" or "Entrance" || !string.IsNullOrEmpty(r.Data.CastleId)))
            if(!castleIds.Contains(r.Data.CastleId))errors.Add($"{r.Data.Path}: CastleId '{r.Data.CastleId}' does not reference an authored castle.");
        foreach(var group in rows.Where(r=>r.Data.Kind=="Entrance").GroupBy(r=>(r.Data.CastleId,r.Data.Role)))
            if(group.Count()>1)errors.Add($"Castle {group.Key.CastleId}: duplicate entrance role '{group.Key.Role}'.");
        foreach(var role in new[]{"main_lane","southern_flank","jungle"})
            if(rows.Count(r=>r.Data.Kind=="Route"&&r.Data.Role==role)!=1)errors.Add($"Routes: exactly one '{role}' region is required.");
        if(rows.Count(r=>r.Data.Kind=="Objective")!=1)errors.Add("Objectives: exactly one objective region is required.");
        if(!rows.Any(r=>r.Data.Kind=="Fixture"))errors.Add("CharacterFixtures: at least one spawn anchor is required.");
        Fail();
        Terrain=floors.ToDictionary(r=>r.Cells.Single(),r=>r.Data.TerrainTag);
        HashSet<GridPosition> Zone(string id,string kind)=>rows.Where(r=>r.Data.Kind==kind&&r.Data.CastleId==id).SelectMany(r=>r.Cells).ToHashSet();
        Castles=rows.Where(r=>r.Data.Kind=="Castle").Select(r=>new BetaCastle(r.Data.Id,r.Data.TeamId,r.Data.IsHome,r.Cells,
            Zone(r.Data.Id,"CaptureZone"),Zone(r.Data.Id,"DeploymentZone"),
            rows.Where(w=>w.Data.Kind=="Obstacle"&&w.Data.CastleId==r.Data.Id&&w.Data.BlocksMovement).SelectMany(w=>w.Cells).ToHashSet(),
            rows.Where(w=>w.Data.Kind=="Entrance"&&w.Data.CastleId==r.Data.Id).ToDictionary(w=>w.Data.Role,w=>(IReadOnlySet<GridPosition>)w.Cells))).ToArray();
        Camps=rows.Where(r=>r.Data.Kind=="Camp").Select(r=>new BetaCamp(r.Data.Id,r.Cells)).ToArray();
        Fixtures=rows.Where(r=>r.Data.Kind=="Fixture").Select(r=>new BetaCharacterFixture(r.Data.Id,r.Data.DisplayName,r.Data.TeamId,r.Cells.Single(),r.Data.ModelPath,r.Data.DefinitionId)).ToArray();
        Objective=rows.Single(r=>r.Data.Kind=="Objective").Cells;
        MainLane=rows.Single(r=>r.Data.Kind=="Route"&&r.Data.Role=="main_lane").Cells;
        SouthernFlank=rows.Single(r=>r.Data.Kind=="Route"&&r.Data.Role=="southern_flank").Cells;
        Jungle=rows.Single(r=>r.Data.Kind=="Route"&&r.Data.Role=="jungle").Cells;
        NatureObstacles=rows.Where(r=>r.Data.Kind=="Obstacle"&&r.Data.CastleId==""&&r.Data.BlocksMovement).SelectMany(r=>r.Cells).ToHashSet();
        Obstacles=rows.Where(r=>(r.Data.Kind is "Obstacle" or "Terrain") && r.Data.BlocksMovement).SelectMany(r=>r.Cells).ToHashSet();
        Connections=rows.Where(r=>r.Data.Kind=="Connection").ToDictionary(r=>r.Data.Id,r=>(IReadOnlySet<GridPosition>)r.Cells);
        foreach(var r in rows.Where(r=>r.Data.Kind=="Obstacle"&&r.Data.CastleId!=""))
            if(!r.Cells.All(Castles.Single(c=>c.Id==r.Data.CastleId).Footprint.Contains))errors.Add($"{r.Data.Path}: castle wall is outside its owning castle footprint.");
        var blocking=rows.Where(r=>(r.Data.Kind is "Obstacle" or "Terrain")&&r.Data.BlocksMovement).SelectMany(r=>r.Cells.Select(c=>(r.Data,c)));
        foreach(var g in blocking.GroupBy(v=>v.c).Where(g=>g.Count()>1))errors.Add($"Blocked cell {g.Key} has duplicate blockers: {string.Join(", ",g.Select(v=>v.Data.Path))}.");
        errors.AddRange(Validate());Fail();
    }

    public static HashSet<GridPosition> Rectangle(int x0,int x1,int y0,int y1) =>
        Enumerable.Range(x0,x1-x0+1).SelectMany(x => Enumerable.Range(y0,y1-y0+1).Select(y => new GridPosition(x,y))).ToHashSet();
    public bool Walkable(GridPosition p) => Board.Contains(p) && !Obstacles.Contains(p);

    /// <summary>Development flood fill, not a gameplay pathfinder or movement policy.</summary>
    public HashSet<GridPosition> Reachable(GridPosition start, IReadOnlySet<GridPosition>? region = null)
    {
        var seen = new HashSet<GridPosition>(); var queue = new Queue<GridPosition>();
        bool Allowed(GridPosition p) => Walkable(p) && (region is null || region.Contains(p));
        if (!Allowed(start)) return seen;
        seen.Add(start); queue.Enqueue(start);
        while (queue.TryDequeue(out var p))
            foreach (var next in new[] {new GridPosition(p.X+1,p.Y),new(p.X-1,p.Y),new(p.X,p.Y+1),new(p.X,p.Y-1)})
                if (Allowed(next) && seen.Add(next)) queue.Enqueue(next);
        return seen;
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        void Check(bool ok,string message) { if (!ok) errors.Add(message); }
        var reachable = Reachable(Fixtures[0].Cell);
        Check(Obstacles.All(Board.Contains),"Out-of-bounds obstacle.");
        if(Source.RequireMirroredLayout)Check(Obstacles.SetEquals(Obstacles.Select(Board.Mirror)),"BoardDefinition.RequireMirroredLayout: obstacles must be mirrored; move the partner or disable this authoring constraint.");
        Check(Fixtures.Select(f=>f.Cell).Distinct().Count()==Fixtures.Count,"Overlapping fixtures.");
        foreach(var f in Fixtures)
        {
            Check(Walkable(f.Cell)&&reachable.Contains(f.Cell),$"CharacterFixtures/{f.CharacterId}: spawn {f.Cell} is blocked or disconnected.");
            Check(!string.IsNullOrWhiteSpace(f.ModelPath),$"CharacterFixtures/{f.CharacterId}: the serialized original model instance is missing.");
            if(Source.RequireDeploymentSpawns)Check(Castles.Any(c=>c.InitialTeamId==f.TeamId&&c.DeploymentCells.Contains(f.Cell)),
                $"CharacterFixtures/{f.CharacterId}: spawn {f.Cell} must be in a deployment zone belonging to team {f.TeamId} (BoardDefinition.RequireDeploymentSpawns).");
        }
        foreach (var c in Castles)
        {
            Check(c.CaptureCells.Count>0&&c.DeploymentCells.Count>0,$"{c.Id}: capture and deployment regions must be nonempty.");
            Check(c.InitialTeamId is 1 or 2||(!c.IsHome&&c.InitialTeamId==0),$"{c.Id}: InitialTeamId must be 1 or 2 (or 0 for a neutral forward castle).");
            Check(c.CaptureCells.Concat(c.DeploymentCells).All(p=>c.Footprint.Contains(p) && Walkable(p) && reachable.Contains(p)), $"Blocked zone: {c.Id}");
            Check(!c.CaptureCells.Overlaps(c.DeploymentCells),$"Overlapping zones: {c.Id}");
            foreach(string direction in new[]{"west","east","north","south"})
                Check(c.Entrances.TryGetValue(direction,out var opening)&&opening.Count>=2&&opening.All(p=>Walkable(p)&&reachable.Contains(p))&&
                    opening.All(Reachable(opening.First(),opening).Contains),$"{c.Id}/{direction}: entrance must have at least two connected, open and accessible cells.");
        }
        var zones = Castles.SelectMany(c=>c.CaptureCells.Concat(c.DeploymentCells)).Concat(Objective).Concat(Camps.SelectMany(c=>c.Cells)).ToArray();
        Check(zones.Distinct().Count()==zones.Length,"Overlapping special regions.");
        Check(Camps.All(c=>c.Cells.All(p=>Walkable(p) && reachable.Contains(p))),"Unreachable camp.");
        Check(Objective.All(p=>Walkable(p) && reachable.Contains(p)),"Unreachable objective.");
        Check(Connections.Values.All(c=>c.All(p=>Walkable(p) && reachable.Contains(p))),"Blocked connection point.");
        var laneStart=MainLane.Where(Walkable).OrderBy(p=>p.X).ThenBy(p=>p.Y).FirstOrDefault();
        Check(MainLane.Count>0&&Reachable(laneStart,MainLane).Any(p=>p.X==MainLane.Max(p=>p.X)),"Disconnected main lane.");
        var flankStart=SouthernFlank.FirstOrDefault(Walkable);
        Check(SouthernFlank.Count>0&&SouthernFlank.Where(Walkable).All(Reachable(flankStart,SouthernFlank).Contains),"Disconnected flank.");
        Check(Board.Cells.Where(Walkable).All(reachable.Contains),"Isolated walkable region.");
        return errors;
    }
}
