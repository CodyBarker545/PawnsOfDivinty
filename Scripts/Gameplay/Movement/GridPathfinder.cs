using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Cell = PawnsOfDivinity.Core.Abilities.GridPosition;

/// <summary>A shortest route including its origin; costs are supplied by the movement map.</summary>
public sealed record TacticalRoute(IReadOnlyList<Cell> Cells, long Cost);

public static class GridPathfinder
{
    /// <summary>Deterministic bounded Dijkstra search for rectangular boards and positive map costs.</summary>
    public static IReadOnlyDictionary<Cell,TacticalRoute> Reachable(Cell start,int width,int height,long allowance,
        Func<Cell,bool> walkable,Func<Cell,Cell,int?> stepCost)
    {
        bool Valid(Cell p)=>p.X>=0&&p.Y>=0&&p.X<width&&p.Y<height&&walkable(p);
        var routes=new Dictionary<Cell,TacticalRoute>();
        if(allowance<0||!Valid(start))return new ReadOnlyDictionary<Cell,TacticalRoute>(routes);
        var costs=new Dictionary<Cell,long>{{start,0}};
        var previous=new Dictionary<Cell,Cell>();
        var queue=new PriorityQueue<Cell,(long Cost,long Order)>();long order=0;
        queue.Enqueue(start,(0,order++));
        // Preserve the legacy pathfinder's tie order: east, south, west, north.
        Cell[] offsets={new(1,0),new(0,1),new(-1,0),new(0,-1)};
        while(queue.TryDequeue(out var current,out var priority))
        {
            if(priority.Cost!=costs[current])continue;
            foreach(var d in offsets)
            {
                var next=new Cell(current.X+d.X,current.Y+d.Y);
                if(!Valid(next))continue;
                var cost=stepCost(current,next);
                if(cost is null||cost<=0||cost>allowance-priority.Cost)continue;
                long total=priority.Cost+cost.Value;
                if(costs.TryGetValue(next,out long known)&&known<=total)continue;
                costs[next]=total;previous[next]=current;queue.Enqueue(next,(total,order++));
            }
        }
        foreach(var entry in costs)
        {
            var path=new List<Cell>{entry.Key};var current=entry.Key;
            while(current!=start){current=previous[current];path.Add(current);}
            path.Reverse();routes[entry.Key]=new(path.AsReadOnly(),entry.Value);
        }
        return new ReadOnlyDictionary<Cell,TacticalRoute>(routes);
    }
    public static List<Vector2I>? Find(Vector2I start, Vector2I goal, int size, HashSet<Vector2I> blocked,
        Func<Vector2I, bool>? walkable = null, Func<Vector2I, Vector2I, bool>? canTraverse = null)
    {
        bool Valid(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < size && p.Y < size &&
            !blocked.Contains(p) && (walkable?.Invoke(p) ?? true);
        if (!Valid(start) || !Valid(goal)) return null;
        var routes=Reachable(new(start.X,start.Y),size,size,int.MaxValue,
            p=>Valid(new(p.X,p.Y)),(from,to)=>canTraverse?.Invoke(new(from.X,from.Y),new(to.X,to.Y))==false?null:1);
        return routes.TryGetValue(new(goal.X,goal.Y),out var route)
            ?route.Cells.Skip(1).Select(p=>new Vector2I(p.X,p.Y)).ToList():null;
    }
}
