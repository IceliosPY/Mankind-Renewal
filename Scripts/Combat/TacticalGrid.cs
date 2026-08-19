using System;
using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Combat;

public partial class TacticalGrid : Node3D
{
    [ExportGroup("Sampling")]
    [Export(PropertyHint.Range, "0.5,4.0,0.25")] public float CellSize { get; set; } = 2.0f;
    [Export] public float MinimumX { get; set; } = -12.0f;
    [Export] public float MaximumX { get; set; } = 12.0f;
    [Export] public float MinimumZ { get; set; } = -12.0f;
    [Export] public float MaximumZ { get; set; } = 12.0f;
    [Export] public float RayTop { get; set; } = 8.0f;
    [Export] public float RayBottom { get; set; } = -2.0f;
    [Export(PropertyHint.Range, "0.1,3.0,0.1")] public float MaximumNeighborHeightDelta { get; set; } = 1.1f;
    [Export(PropertyHint.Range, "0.0,60.0,1.0")] public float MaximumSlopeDegrees { get; set; } = 35.0f;
    [Export(PropertyHint.Layers3DPhysics)] public uint WorldCollisionMask { get; set; } = 1;

    [ExportGroup("Walkable surfaces")]
    [Export] public NodePath MainFloorPath { get; set; } = new();
    [Export] public NodePath UpperFloorPath { get; set; } = new();
    [Export] public NodePath RampLeftPath { get; set; } = new();
    [Export] public NodePath RampRightPath { get; set; } = new();

    [ExportGroup("Unit clearance")]
    [Export] public float UnitRadius { get; set; } = 0.45f;
    [Export] public float UnitHeight { get; set; } = 1.8f;

    [ExportGroup("Lifecycle")]
    [Export] public bool AutoBuild { get; set; } = true;

    public bool IsBuilt { get; private set; }
    public IReadOnlyList<TacticalCell> Cells => _cells;

    private readonly List<TacticalCell> _cells = new();
    private readonly Dictionary<(int X, int Z), List<TacticalCell>> _columns = new();
    private readonly HashSet<CollisionObject3D> _walkableSurfaces = new();
    private readonly Godot.Collections.Array<Rid> _surfaceRids = new();
    private readonly HashSet<TacticalCell> _pathCells = new();
    private TacticalCell? _hoveredCell;
    private TacticalCell? _currentCell;
    private TacticalCell? _destinationCell;

    private static readonly Color DefaultColor = new(0.08f, 0.58f, 0.82f, 0.34f);
    private static readonly Color PathColor = new(0.14f, 0.48f, 1.0f, 0.55f);
    private static readonly Color CurrentColor = new(0.12f, 0.95f, 0.4f, 0.64f);
    private static readonly Color DestinationColor = new(1.0f, 0.28f, 0.58f, 0.72f);
    private static readonly Color HoverColor = new(1.0f, 0.78f, 0.16f, 0.72f);

    public override void _Ready()
    {
        Visible = false;
        SetPhysicsProcess(AutoBuild);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsBuilt || !AutoBuild)
            return;

        BuildGrid();
        SetPhysicsProcess(false);
    }

    public bool BuildGrid()
    {
        if (IsBuilt)
            return true;

        if (!ResolveWalkableSurfaces())
        {
            GD.PushError("TacticalGrid: une ou plusieurs surfaces praticables sont introuvables.");
            return false;
        }

        World3D world = GetWorld3D();
        PhysicsDirectSpaceState3D spaceState = world.DirectSpaceState;
        int gridX = 0;
        for (float x = MinimumX + CellSize * 0.5f; x < MaximumX; x += CellSize, gridX++)
        {
            int gridZ = 0;
            for (float z = MinimumZ + CellSize * 0.5f; z < MaximumZ; z += CellSize, gridZ++)
            {
                if (!TrySampleSurface(spaceState, x, z, out Vector3 position, out Vector3 normal))
                    continue;
                if (!HasUnitClearance(spaceState, position))
                    continue;

                CreateCell(gridX, gridZ, position, normal);
            }
        }

        ConnectNeighbors(spaceState);
        IsBuilt = _cells.Count > 0;
        if (!IsBuilt)
            GD.PushError("TacticalGrid: aucune cellule praticable n'a été créée.");
        else
            GD.Print($"TacticalGrid: {_cells.Count} cellules créées à {CellSize:0.##} m.");
        return IsBuilt;
    }

    public TacticalCell? GetNearestCell(Vector3 worldPosition, float maximumDistance = float.MaxValue)
    {
        TacticalCell? nearest = null;
        float nearestDistance = maximumDistance;
        foreach (TacticalCell cell in _cells)
        {
            float distance = cell.WorldPosition.DistanceTo(worldPosition);
            if (distance >= nearestDistance)
                continue;
            nearest = cell;
            nearestDistance = distance;
        }
        return nearest;
    }

    public TacticalCell? GetCellById(int cellId)
    {
        return cellId >= 0 && cellId < _cells.Count ? _cells[cellId] : null;
    }

    public int GetCellCount() => _cells.Count;

    public bool GetIsBuilt() => IsBuilt;

    public int GetCellIdNearWorld(Vector3 worldPosition, float maximumDistance)
    {
        return GetNearestCell(worldPosition, maximumDistance)?.CellId ?? -1;
    }

    public int GetCellNeighborCount(int cellId) => GetCellById(cellId)?.Neighbors.Count ?? 0;

    public float GetCellSurfaceHeight(int cellId) => GetCellById(cellId)?.SurfaceHeight ?? float.NaN;

    public bool GetCellIsOccupied(int cellId) => GetCellById(cellId)?.IsOccupied ?? false;

    public int GetPathLengthBetweenCells(int fromCellId, int toCellId)
    {
        TacticalCell? from = GetCellById(fromCellId);
        TacticalCell? to = GetCellById(toCellId);
        return from is null || to is null ? 0 : TacticalPathfinder.FindPath(from, to).Count;
    }

    public int GetHoveredCellId() => _hoveredCell?.CellId ?? -1;

    public int GetCurrentCellId() => _currentCell?.CellId ?? -1;

    public Vector3 GetCellWorldPosition(int cellId) => GetCellById(cellId)?.WorldPosition ?? Vector3.Zero;

    public bool SetHoveredCellFromWorld(Vector3 worldPosition)
    {
        TacticalCell? candidate = GetNearestCell(worldPosition, CellSize * 0.9f);
        if (candidate == _hoveredCell)
            return candidate is not null;
        _hoveredCell = candidate;
        RefreshDebugColors();
        return candidate is not null;
    }

    public void ClearHoveredCell()
    {
        _hoveredCell = null;
        RefreshDebugColors();
    }

    public void SetCurrentCell(TacticalCell? cell)
    {
        _currentCell = cell;
        RefreshDebugColors();
    }

    public void ShowPath(IReadOnlyList<TacticalCell> path)
    {
        _pathCells.Clear();
        foreach (TacticalCell cell in path)
            _pathCells.Add(cell);
        _destinationCell = path.Count > 0 ? path[^1] : null;
        RefreshDebugColors();
    }

    public void ClearPath()
    {
        _pathCells.Clear();
        _destinationCell = null;
        RefreshDebugColors();
    }

    private bool ResolveWalkableSurfaces()
    {
        NodePath[] paths = { MainFloorPath, UpperFloorPath, RampLeftPath, RampRightPath };
        foreach (NodePath path in paths)
        {
            CollisionObject3D? surface = GetNodeOrNull<CollisionObject3D>(path);
            if (surface is null)
                return false;
            _walkableSurfaces.Add(surface);
            _surfaceRids.Add(surface.GetRid());
        }
        return true;
    }

    private bool TrySampleSurface(
        PhysicsDirectSpaceState3D spaceState,
        float x,
        float z,
        out Vector3 position,
        out Vector3 normal)
    {
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            new Vector3(x, RayTop, z), new Vector3(x, RayBottom, z), WorldCollisionMask);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        Godot.Collections.Dictionary hit = spaceState.IntersectRay(query);
        if (hit.Count == 0)
        {
            position = Vector3.Zero;
            normal = Vector3.Up;
            return false;
        }

        CollisionObject3D? collider = hit["collider"].AsGodotObject() as CollisionObject3D;
        normal = hit["normal"].AsVector3().Normalized();
        position = hit["position"].AsVector3();
        float minimumUpDot = Mathf.Cos(Mathf.DegToRad(MaximumSlopeDegrees));
        return collider is not null && _walkableSurfaces.Contains(collider) && normal.Dot(Vector3.Up) >= minimumUpDot;
    }

    private bool HasUnitClearance(PhysicsDirectSpaceState3D spaceState, Vector3 surfacePosition)
    {
        CapsuleShape3D capsule = new() { Radius = UnitRadius, Height = UnitHeight };
        PhysicsShapeQueryParameters3D query = new()
        {
            Shape = capsule,
            Transform = new Transform3D(Basis.Identity, surfacePosition + Vector3.Up * UnitHeight * 0.5f),
            CollisionMask = WorldCollisionMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Exclude = _surfaceRids,
        };
        return spaceState.IntersectShape(query, 1).Count == 0;
    }

    private void CreateCell(int gridX, int gridZ, Vector3 worldPosition, Vector3 normal)
    {
        TacticalCell cell = new()
        {
            Name = $"Cell_{gridX}_{gridZ}_H{Mathf.RoundToInt(worldPosition.Y * 2.0f)}",
            CellId = _cells.Count,
            GridX = gridX,
            GridZ = gridZ,
            HeightLayer = Mathf.RoundToInt(worldPosition.Y * 2.0f),
            SurfaceHeight = worldPosition.Y,
            Walkable = true,
            WorldPosition = worldPosition,
            Position = ToLocal(worldPosition),
        };
        AddChild(cell);
        cell.ConfigureDebugVisual(CellSize, normal);
        _cells.Add(cell);

        (int X, int Z) key = (gridX, gridZ);
        if (!_columns.TryGetValue(key, out List<TacticalCell>? column))
        {
            column = new List<TacticalCell>();
            _columns[key] = column;
        }
        column.Add(cell);
    }

    private void ConnectNeighbors(PhysicsDirectSpaceState3D spaceState)
    {
        ReadOnlySpan<(int X, int Z)> directions = stackalloc (int X, int Z)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1),
        };

        foreach (TacticalCell cell in _cells)
        {
            foreach ((int offsetX, int offsetZ) in directions)
            {
                if (!_columns.TryGetValue((cell.GridX + offsetX, cell.GridZ + offsetZ), out List<TacticalCell>? candidates))
                    continue;

                foreach (TacticalCell candidate in candidates)
                {
                    if (Mathf.Abs(candidate.SurfaceHeight - cell.SurfaceHeight) > MaximumNeighborHeightDelta)
                        continue;
                    if (!HasTransitionClearance(spaceState, cell, candidate))
                        continue;
                    cell.AddNeighbor(candidate);
                }
            }
        }
    }

    private bool HasTransitionClearance(
        PhysicsDirectSpaceState3D spaceState,
        TacticalCell from,
        TacticalCell to)
    {
        Vector3 midpoint = from.WorldPosition.Lerp(to.WorldPosition, 0.5f);
        return HasUnitClearance(spaceState, midpoint);
    }

    private void RefreshDebugColors()
    {
        foreach (TacticalCell cell in _cells)
        {
            Color color = DefaultColor;
            if (_pathCells.Contains(cell))
                color = PathColor;
            if (cell == _destinationCell)
                color = DestinationColor;
            if (cell == _currentCell)
                color = CurrentColor;
            if (cell == _hoveredCell)
                color = HoverColor;
            cell.SetDebugColor(color);
        }
    }
}
