using System.Collections.Generic;
using Godot;
using MankindRenewal.Characters;

namespace MankindRenewal.Combat;

public partial class TacticalUnit : Node
{
    [Export] public NodePath PlayerPath { get; set; } = new();
    [Export(PropertyHint.Range, "0.5,12.0,0.1")] public float MovementSpeed { get; set; } = 4.0f;
    [Export] public float PlayerCenterHeight { get; set; } = 0.9f;

    public TacticalCell? CurrentCell { get; private set; }
    public bool IsCombatActive { get; private set; }
    public bool IsMoving => _pathIndex < _path.Count;
    public int CompletedStepCount { get; private set; }

    public event System.Action<TacticalCell>? CellReached;
    public event System.Action? PathCompleted;

    private PlayerController _player = null!;
    private readonly List<TacticalCell> _path = new();
    private int _pathIndex;
    private Vector3 _segmentStart;
    private float _segmentProgress;

    public override void _Ready()
    {
        _player = GetNode<PlayerController>(PlayerPath);
        SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsCombatActive || !IsMoving)
            return;

        TacticalCell targetCell = _path[_pathIndex];
        Vector3 targetPosition = GetPlayerPosition(targetCell);
        float segmentLength = Mathf.Max(_segmentStart.DistanceTo(targetPosition), 0.001f);
        _segmentProgress += MovementSpeed * (float)delta / segmentLength;
        _player.GlobalPosition = _segmentStart.Lerp(targetPosition, Mathf.Min(_segmentProgress, 1.0f));

        if (_segmentProgress < 1.0f)
            return;

        CurrentCell?.Vacate(_player);
        CurrentCell = targetCell;
        CurrentCell.TryOccupy(_player);
        CompletedStepCount++;
        CellReached?.Invoke(CurrentCell);
        _pathIndex++;
        _segmentStart = _player.GlobalPosition;
        _segmentProgress = 0.0f;

        if (!IsMoving)
        {
            _path.Clear();
            _pathIndex = 0;
            PathCompleted?.Invoke();
        }
    }

    public bool EnterCombat(TacticalCell startCell)
    {
        if (IsCombatActive || !startCell.Walkable || !startCell.TryOccupy(_player))
            return false;

        _player.CancelAutoMovement();
        _player.Velocity = Vector3.Zero;
        _player.SetPhysicsProcess(false);
        _player.GlobalPosition = GetPlayerPosition(startCell);
        CurrentCell = startCell;
        IsCombatActive = true;
        CompletedStepCount = 0;
        SetPhysicsProcess(true);
        return true;
    }

    public bool FollowPath(IReadOnlyList<TacticalCell> path)
    {
        if (!IsCombatActive || CurrentCell is null || path.Count == 0 || path[0] != CurrentCell)
            return false;

        CancelPath();
        for (int index = 1; index < path.Count; index++)
            _path.Add(path[index]);
        _pathIndex = 0;
        _segmentStart = _player.GlobalPosition;
        _segmentProgress = 0.0f;
        if (_path.Count == 0)
            PathCompleted?.Invoke();
        return true;
    }

    public void CancelPath()
    {
        _path.Clear();
        _pathIndex = 0;
        _segmentProgress = 0.0f;
    }

    public void ExitCombat()
    {
        if (!IsCombatActive)
            return;

        CancelPath();
        CurrentCell?.Vacate(_player);
        CurrentCell = null;
        IsCombatActive = false;
        _player.Velocity = Vector3.Zero;
        _player.SetPhysicsProcess(true);
        SetPhysicsProcess(false);
    }

    public int GetCurrentCellId() => CurrentCell?.CellId ?? -1;

    public int GetCompletedStepCount() => CompletedStepCount;

    public bool GetIsMoving() => IsMoving;

    private Vector3 GetPlayerPosition(TacticalCell cell)
    {
        return cell.WorldPosition + Vector3.Up * PlayerCenterHeight;
    }
}
