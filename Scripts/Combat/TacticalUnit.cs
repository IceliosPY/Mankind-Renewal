using System;
using System.Collections.Generic;
using Godot;
using MankindRenewal.Characters;

namespace MankindRenewal.Combat;

public partial class TacticalUnit : Node
{
    [ExportGroup("Actor")]
    [Export] public NodePath PlayerPath { get; set; } = new();
    [Export] public NodePath ActorPath { get; set; } = new();
    [Export] public string UnitDisplayName { get; set; } = "Tactical Unit";
    [Export] public bool StartAsParticipant { get; set; } = true;
    [Export(PropertyHint.Range, "0.5,12.0,0.1")] public float MovementSpeed { get; set; } = 4.0f;
    [Export] public float PlayerCenterHeight { get; set; } = 0.9f;

    [ExportGroup("Turn economy")]
    [Export] public bool UseTurnEconomy { get; set; }
    [Export]
    public int Initiative
    {
        get => _initiative;
        set
        {
            if (_initiative == value)
                return;
            _initiative = value;
            InitiativeChanged?.Invoke(this);
        }
    }
    [Export(PropertyHint.Range, "0,20,1")] public int MaxActionPoints { get; set; } = 2;
    [Export(PropertyHint.Range, "0,30,1")] public int MaxMovementPoints { get; set; } = 6;
    [Export(PropertyHint.Range, "1,10,1")] public int DefaultTransitionCost { get; set; } = 1;
    [Export] public int CurrentActionPoints { get; private set; }
    [Export] public int CurrentMovementPoints { get; private set; }
    [Export] public bool HasActedThisRound { get; private set; }
    [Export] public bool IsActiveTurn { get; private set; }

    public TacticalCell? CurrentCell { get; private set; }
    public bool IsCombatActive { get; private set; }
    public bool IsMoving => _pathIndex < _path.Count;
    public int CompletedStepCount { get; private set; }
    public Node3D Actor => _actor;

    public event Action<TacticalUnit>? InitiativeChanged;
    public event Action<TacticalUnit>? ResourcesChanged;
    public event Action<TacticalCell>? CellReached;
    public event Action? PathCompleted;

    private int _initiative = 10;
    private Node3D _actor = null!;
    private PlayerController? _player;
    private readonly List<TacticalCell> _path = new();
    private int _pathIndex;
    private Vector3 _segmentStart;
    private float _segmentProgress;

    public override void _Ready()
    {
        NodePath resolvedPath = !ActorPath.IsEmpty ? ActorPath : PlayerPath;
        _actor = GetNode<Node3D>(resolvedPath);
        _player = _actor as PlayerController;
        AddToGroup("tactical_units");
        SetPhysicsProcess(false);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsCombatActive || !IsMoving)
            return;

        TacticalCell targetCell = _path[_pathIndex];
        int transitionCost = CurrentCell is null ? 0 : GetMovementCost(CurrentCell, targetCell);
        if (UseTurnEconomy && CurrentMovementPoints < transitionCost)
        {
            CancelPathAndReturnToCurrentCell();
            PathCompleted?.Invoke();
            return;
        }

        Vector3 targetPosition = GetActorPosition(targetCell);
        float segmentLength = Mathf.Max(_segmentStart.DistanceTo(targetPosition), 0.001f);
        _segmentProgress += MovementSpeed * (float)delta / segmentLength;
        _actor.GlobalPosition = _segmentStart.Lerp(targetPosition, Mathf.Min(_segmentProgress, 1.0f));

        if (_segmentProgress < 1.0f)
            return;

        if (UseTurnEconomy)
            SpendMovementPoints(transitionCost);
        CurrentCell?.Vacate(_actor);
        CurrentCell = targetCell;
        CurrentCell.TryOccupy(_actor);
        CompletedStepCount++;
        CellReached?.Invoke(CurrentCell);
        _pathIndex++;
        _segmentStart = _actor.GlobalPosition;
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
        if (IsCombatActive || !startCell.Walkable || !startCell.TryOccupy(_actor))
            return false;

        if (_player is not null)
        {
            _player.CancelAutoMovement();
            _player.Velocity = Vector3.Zero;
            _player.SetPhysicsProcess(false);
        }

        _actor.GlobalPosition = GetActorPosition(startCell);
        CurrentCell = startCell;
        IsCombatActive = true;
        CompletedStepCount = 0;
        CurrentActionPoints = 0;
        CurrentMovementPoints = 0;
        IsActiveTurn = false;
        HasActedThisRound = false;
        SetPhysicsProcess(true);
        ResourcesChanged?.Invoke(this);
        return true;
    }

    public bool FollowPath(IReadOnlyList<TacticalCell> path)
    {
        if (!IsCombatActive || CurrentCell is null || path.Count == 0 || path[0] != CurrentCell)
            return false;
        if (UseTurnEconomy && !IsActiveTurn)
            return false;

        int totalCost = GetPathMovementCost(path);
        if (UseTurnEconomy && totalCost > CurrentMovementPoints)
            return false;

        CancelPath();
        for (int index = 1; index < path.Count; index++)
            _path.Add(path[index]);
        _pathIndex = 0;
        _segmentStart = _actor.GlobalPosition;
        _segmentProgress = 0.0f;
        if (_path.Count == 0)
            PathCompleted?.Invoke();
        return true;
    }

    public virtual int GetMovementCost(TacticalCell from, TacticalCell to)
    {
        return Mathf.Max(DefaultTransitionCost, 1);
    }

    public int GetPathMovementCost(IReadOnlyList<TacticalCell> path)
    {
        int total = 0;
        for (int index = 1; index < path.Count; index++)
            total += GetMovementCost(path[index - 1], path[index]);
        return total;
    }

    public void BeginTurn()
    {
        IsActiveTurn = true;
        CurrentActionPoints = Mathf.Max(MaxActionPoints, 0);
        CurrentMovementPoints = Mathf.Max(MaxMovementPoints, 0);
        ResourcesChanged?.Invoke(this);
    }

    public void EndTurn()
    {
        CancelPathAndReturnToCurrentCell();
        IsActiveTurn = false;
        CurrentActionPoints = 0;
        CurrentMovementPoints = 0;
        HasActedThisRound = true;
        ResourcesChanged?.Invoke(this);
    }

    public void PrepareForNewRound()
    {
        HasActedThisRound = false;
        IsActiveTurn = false;
        CurrentActionPoints = 0;
        CurrentMovementPoints = 0;
        ResourcesChanged?.Invoke(this);
    }

    public bool SpendActionPoints(int amount = 1)
    {
        if (!IsActiveTurn || amount <= 0 || CurrentActionPoints < amount)
            return false;
        CurrentActionPoints -= amount;
        ResourcesChanged?.Invoke(this);
        return true;
    }

    public bool SpendMovementPoints(int amount)
    {
        if (!IsActiveTurn || amount <= 0 || CurrentMovementPoints < amount)
            return false;
        CurrentMovementPoints -= amount;
        ResourcesChanged?.Invoke(this);
        return true;
    }

    public void CancelPath()
    {
        _path.Clear();
        _pathIndex = 0;
        _segmentProgress = 0.0f;
    }

    public void CancelPathAndReturnToCurrentCell()
    {
        CancelPath();
        if (CurrentCell is not null)
            _actor.GlobalPosition = GetActorPosition(CurrentCell);
    }

    public void ExitCombat()
    {
        if (!IsCombatActive)
            return;

        CancelPath();
        CurrentCell?.Vacate(_actor);
        CurrentCell = null;
        IsCombatActive = false;
        IsActiveTurn = false;
        CurrentActionPoints = 0;
        CurrentMovementPoints = 0;
        _player?.SetPhysicsProcess(true);
        if (_player is not null)
            _player.Velocity = Vector3.Zero;
        SetPhysicsProcess(false);
        ResourcesChanged?.Invoke(this);
    }

    public void SetActorVisible(bool visible) => _actor.Visible = visible;

    public int GetCurrentCellId() => CurrentCell?.CellId ?? -1;
    public int GetCompletedStepCount() => CompletedStepCount;
    public bool GetIsMoving() => IsMoving;
    public int GetInitiative() => Initiative;
    public void SetInitiative(int value) => Initiative = value;
    public int GetCurrentActionPoints() => CurrentActionPoints;
    public int GetCurrentMovementPoints() => CurrentMovementPoints;
    public int GetMaxActionPoints() => MaxActionPoints;
    public int GetMaxMovementPoints() => MaxMovementPoints;
    public bool GetHasActedThisRound() => HasActedThisRound;
    public bool GetIsActiveTurn() => IsActiveTurn;
    public string GetUnitDisplayName() => UnitDisplayName;
    public Vector3 GetActorWorldPosition() => _actor.GlobalPosition;

    private Vector3 GetActorPosition(TacticalCell cell)
    {
        return cell.WorldPosition + Vector3.Up * PlayerCenterHeight;
    }
}
