using System;
using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Combat;

public partial class TacticalCell : Node3D
{
    [Export] public int CellId { get; set; }
    [Export] public int GridX { get; set; }
    [Export] public int GridZ { get; set; }
    [Export] public int HeightLayer { get; set; }
    [Export] public float SurfaceHeight { get; set; }
    [Export] public bool Walkable { get; set; } = true;
    [Export] public bool IsOccupied { get; private set; }
    [Export] public int[] NeighborIds { get; private set; } = Array.Empty<int>();

    public Vector3 WorldPosition { get; set; }
    public Vector3 SurfaceNormal { get; set; } = Vector3.Up;
    public Node3D? Occupant { get; private set; }
    public List<TacticalCell> Neighbors { get; } = new();

    private MeshInstance3D? _debugMesh;
    private StandardMaterial3D? _debugMaterial;

    public void ConfigureDebugVisual(float cellSize, Vector3 normal)
    {
        SurfaceNormal = normal.Normalized();
        Basis = new Basis(new Quaternion(Vector3.Up, SurfaceNormal));

        _debugMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.08f, 0.58f, 0.82f, 0.34f),
            EmissionEnabled = true,
            Emission = new Color(0.04f, 0.34f, 0.58f),
        };

        _debugMesh = new MeshInstance3D
        {
            Name = "DebugTile",
            Position = Vector3.Up * 0.045f,
            Mesh = new BoxMesh { Size = new Vector3(cellSize * 0.88f, 0.025f, cellSize * 0.88f) },
            MaterialOverride = _debugMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_debugMesh);
    }

    public void SetDebugColor(Color color)
    {
        if (_debugMaterial is null)
            return;

        _debugMaterial.AlbedoColor = color;
        _debugMaterial.Emission = new Color(color.R, color.G, color.B) * 0.7f;
    }

    public void AddNeighbor(TacticalCell neighbor)
    {
        if (neighbor == this || Neighbors.Contains(neighbor))
            return;

        Neighbors.Add(neighbor);
        int[] ids = NeighborIds;
        Array.Resize(ref ids, ids.Length + 1);
        ids[^1] = neighbor.CellId;
        NeighborIds = ids;
    }

    public bool TryOccupy(Node3D occupant)
    {
        if (IsOccupied && Occupant != occupant)
            return false;

        Occupant = occupant;
        IsOccupied = true;
        return true;
    }

    public void Vacate(Node3D occupant)
    {
        if (Occupant != occupant)
            return;

        Occupant = null;
        IsOccupied = false;
    }
}
