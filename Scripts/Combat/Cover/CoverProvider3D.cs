using Godot;

namespace MankindRenewal.Combat.Cover;

public partial class CoverProvider3D : Node3D
{
    [Export] public bool TacticalEnabled { get; set; } = true;
    [Export] public CoverLevel Level { get; set; } = CoverLevel.Light;
    [Export] public CoverDirection ProtectedDirection { get; set; } = CoverDirection.North;
    [Export] public bool BlocksLineOfFire { get; set; }
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float ObstructionRadius { get; set; } = 0.75f;
    [Export] public bool ShowDebugMarker { get; set; } = true;

    private TacticalCell? _cachedCell;
    private Label3D? _label;

    public override void _Ready()
    {
        _label = GetNodeOrNull<Label3D>("CoverLabelDebug");
        AddToGroup("cover_providers");
    }

    public override void _ExitTree()
    {
        _cachedCell = null;
        _label = null;
    }

    public TacticalCell? ResolveProtectedCell(TacticalGrid grid)
    {
        if (_cachedCell is null || _cachedCell.GetParent() is null)
            _cachedCell = grid.GetNearestCell(GlobalPosition, grid.CellSize * 0.7f);
        EnsureDebugMarker(grid);
        return _cachedCell;
    }

    public void InvalidateCellAssociation() => _cachedCell = null;

    public Vector3 GetEdgeWorldPosition(TacticalGrid grid)
    {
        TacticalCell? cell = ResolveProtectedCell(grid);
        if (cell is null)
            return GlobalPosition;
        return cell.WorldPosition + DirectionVector(ProtectedDirection) * (grid.CellSize * 0.5f);
    }

    public int GetCoverLevelValue() => (int)Level;
    public int GetProtectedDirectionValue() => (int)ProtectedDirection;
    public int GetProtectedCellId(TacticalGrid grid) => ResolveProtectedCell(grid)?.CellId ?? -1;

    public static Vector3 DirectionVector(CoverDirection direction) => direction switch
    {
        CoverDirection.North => Vector3.Forward,
        CoverDirection.East => Vector3.Right,
        CoverDirection.South => Vector3.Back,
        _ => Vector3.Left,
    };

    private void EnsureDebugMarker(TacticalGrid grid)
    {
        if (!ShowDebugMarker)
        {
            if (_label is not null)
                _label.Visible = false;
            return;
        }

        if (_label is null)
            return;

        Color color = Level switch
        {
            CoverLevel.Light => new Color(0.96f, 0.78f, 0.12f),
            CoverLevel.Heavy => new Color(1.0f, 0.32f, 0.12f),
            CoverLevel.Total => new Color(0.78f, 0.08f, 0.18f),
            _ => new Color(0.65f, 0.72f, 0.8f),
        };
        Vector3 localDirection = DirectionVector(ProtectedDirection);
        Vector3 edgePosition = (_cachedCell?.WorldPosition ?? GlobalPosition) + localDirection * grid.CellSize * 0.46f;
        _label.Text = $"[ {ProtectedDirection.ToString().ToUpperInvariant()} = {Level.ToString().ToUpperInvariant()} ]";
        _label.Modulate = color;
        _label.GlobalPosition = edgePosition + Vector3.Up * 0.42f;
        _label.Visible = TacticalEnabled;
    }
}
