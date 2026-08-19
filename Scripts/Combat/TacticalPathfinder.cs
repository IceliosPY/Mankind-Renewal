using System;
using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Combat;

public static class TacticalPathfinder
{
    public static List<TacticalCell> FindPath(TacticalCell start, TacticalCell goal)
    {
        if (start == goal)
            return new List<TacticalCell> { start };

        PriorityQueue<TacticalCell, float> frontier = new();
        Dictionary<TacticalCell, TacticalCell?> cameFrom = new() { [start] = null };
        Dictionary<TacticalCell, float> costSoFar = new() { [start] = 0.0f };
        frontier.Enqueue(start, 0.0f);

        while (frontier.Count > 0)
        {
            TacticalCell current = frontier.Dequeue();
            if (current == goal)
                return ReconstructPath(cameFrom, goal);

            foreach (TacticalCell neighbor in current.Neighbors)
            {
                if (!neighbor.Walkable || (neighbor.IsOccupied && neighbor != goal))
                    continue;

                float stepCost = current.WorldPosition.DistanceTo(neighbor.WorldPosition);
                float newCost = costSoFar[current] + stepCost;
                if (costSoFar.TryGetValue(neighbor, out float previousCost) && newCost >= previousCost)
                    continue;

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                float priority = newCost + Heuristic(neighbor, goal);
                frontier.Enqueue(neighbor, priority);
            }
        }

        return new List<TacticalCell>();
    }

    private static float Heuristic(TacticalCell from, TacticalCell to)
    {
        int horizontal = Math.Abs(from.GridX - to.GridX) + Math.Abs(from.GridZ - to.GridZ);
        return horizontal + Math.Abs(from.SurfaceHeight - to.SurfaceHeight);
    }

    private static List<TacticalCell> ReconstructPath(
        IReadOnlyDictionary<TacticalCell, TacticalCell?> cameFrom,
        TacticalCell goal)
    {
        List<TacticalCell> result = new();
        TacticalCell? current = goal;
        while (current is not null)
        {
            result.Add(current);
            current = cameFrom[current];
        }

        result.Reverse();
        return result;
    }
}
