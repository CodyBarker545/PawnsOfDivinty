using Godot;
using System;
using System.Collections.Generic;

public static class GridPathfinder
{
    public static List<Vector2I>? Find(Vector2I start, Vector2I goal, int size, HashSet<Vector2I> blocked,
        Func<Vector2I, bool>? walkable = null, Func<Vector2I, Vector2I, bool>? canTraverse = null)
    {
        bool Valid(Vector2I p) => p.X >= 0 && p.Y >= 0 && p.X < size && p.Y < size &&
            !blocked.Contains(p) && (walkable?.Invoke(p) ?? true);
        if (!Valid(start) || !Valid(goal)) return null;
        var queue = new Queue<Vector2I>();
        var previous = new Dictionary<Vector2I, Vector2I> { [start] = start };
        queue.Enqueue(start);
        Vector2I[] offsets = { Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                var path = new List<Vector2I>();
                while (current != start) { path.Add(current); current = previous[current]; }
                path.Reverse();
                return path;
            }
            foreach (var offset in offsets)
            {
                var next = current + offset;
                if (Valid(next) && !previous.ContainsKey(next) && (canTraverse?.Invoke(current, next) ?? true))
                { previous[next] = current; queue.Enqueue(next); }
            }
        }
        return null;
    }
}
