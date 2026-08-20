using System;
using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Combat;

public static class TacticalPathfinder
{
    public static List<TacticalCell> FindPath(TacticalCell start, TacticalCell goal)
    {
        return FindPath(start, goal, (from, to) => from.WorldPosition.DistanceTo(to.WorldPosition));
    }

    public static List<TacticalCell> FindPath(
        TacticalCell start,
        TacticalCell goal,
        Func<TacticalCell, TacticalCell, float> transitionCost)
    {
        if (start == goal)
            return new List<TacticalCell> { start };
        if (goal.IsOccupied)
            return new List<TacticalCell>();

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
                if (!neighbor.Walkable || (neighbor.IsOccupied && neighbor != start))
                    continue;

                float newCost = costSoFar[current] + Mathf.Max(transitionCost(current, neighbor), 0.0f);
                if (costSoFar.TryGetValue(neighbor, out float previousCost) && newCost >= previousCost)
                    continue;

                costSoFar[neighbor] = newCost;
                cameFrom[neighbor] = current;
                frontier.Enqueue(neighbor, newCost + Heuristic(neighbor, goal));
            }
        }

        return new List<TacticalCell>();
    }

    public static Dictionary<TacticalCell, int> FindReachableCells(
        TacticalCell start,
        int movementBudget,
        Func<TacticalCell, TacticalCell, int> transitionCost)
    {
        Dictionary<TacticalCell, int> costs = new() { [start] = 0 };
        PriorityQueue<TacticalCell, int> frontier = new();
        frontier.Enqueue(start, 0);

        while (frontier.Count > 0)
        {
            TacticalCell current = frontier.Dequeue();
            int currentCost = costs[current];
            foreach (TacticalCell neighbor in current.Neighbors)
            {
                if (!neighbor.Walkable || (neighbor.IsOccupied && neighbor != start))
                    continue;
                int newCost = currentCost + Mathf.Max(transitionCost(current, neighbor), 1);
                if (newCost > movementBudget)
                    continue;
                if (costs.TryGetValue(neighbor, out int previousCost) && newCost >= previousCost)
                    continue;
                costs[neighbor] = newCost;
                frontier.Enqueue(neighbor, newCost);
            }
        }

        return costs;
    }

    private static float Heuristic(TacticalCell from, TacticalCell to)
    {
        return Math.Abs(from.GridX - to.GridX) + Math.Abs(from.GridZ - to.GridZ);
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
