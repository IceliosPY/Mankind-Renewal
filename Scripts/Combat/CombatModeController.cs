using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Characters;
using MankindRenewal.Systems;

namespace MankindRenewal.Combat;

public partial class CombatModeController : Node
{
    [Export] public NodePath GridPath { get; set; } = new();
    [Export] public NodePath TacticalUnitPath { get; set; } = new();
    [Export] public NodePath PlayerPath { get; set; } = new();
    [Export] public NodePath GameModeManagerPath { get; set; } = new();
    [Export] public NodePath CameraPath { get; set; } = new();
    [Export] public NodePath TargetSelectionHandlerPath { get; set; } = new();
    [Export] public NodePath MovementActionHandlerPath { get; set; } = new();
    [Export(PropertyHint.Range, "0.5,5.0,0.1")] public float MaximumEntrySnapDistance { get; set; } = 2.2f;
    [Export] public float PlayerCenterHeight { get; set; } = 0.9f;
    [Export] public float MouseRayLength { get; set; } = 1000.0f;

    public bool IsCombatActive { get; private set; }
    public int LastPathLength { get; private set; }
    public int LastPathCost { get; private set; }

    private TacticalGrid _grid = null!;
    private TacticalUnit _primaryUnit = null!;
    private PlayerController _player = null!;
    private GameModeManager _gameModeManager = null!;
    private Camera3D _camera = null!;
    private TurnManager? _turnManager;
    private ICombatTargetSelectionHandler? _targetSelectionHandler;
    private ICombatMovementActionHandler? _movementActionHandler;
    private readonly List<TacticalUnit> _engagedUnits = new();
    private readonly HashSet<TacticalUnit> _subscribedUnits = new();

    public override void _Ready()
    {
        _grid = GetNode<TacticalGrid>(GridPath);
        _primaryUnit = GetNode<TacticalUnit>(TacticalUnitPath);
        _player = GetNode<PlayerController>(PlayerPath);
        _gameModeManager = GetNode<GameModeManager>(GameModeManagerPath);
        _camera = GetNode<Camera3D>(CameraPath);
        _turnManager = GetTree().GetFirstNodeInGroup("turn_manager") as TurnManager;
        if (!TargetSelectionHandlerPath.IsEmpty)
            _targetSelectionHandler = GetNodeOrNull(TargetSelectionHandlerPath) as ICombatTargetSelectionHandler;
        if (!MovementActionHandlerPath.IsEmpty)
            _movementActionHandler = GetNodeOrNull(MovementActionHandlerPath) as ICombatMovementActionHandler;
        SubscribeUnit(_primaryUnit);
        if (_turnManager is not null)
        {
            _turnManager.TurnStarted += OnTurnStarted;
            _turnManager.OrderChanged += OnOrderChanged;
        }
    }

    public override void _ExitTree()
    {
        foreach (TacticalUnit unit in _subscribedUnits.ToArray())
            UnsubscribeUnit(unit);
        if (_turnManager is not null)
        {
            _turnManager.TurnStarted -= OnTurnStarted;
            _turnManager.OrderChanged -= OnOrderChanged;
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("toggle_combat"))
        {
            if (IsCombatActive)
                ExitCombat();
            else
                EnterCombat();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!IsCombatActive)
            return;

        if (inputEvent.IsActionPressed("cancel_combat"))
        {
            ExitCombat();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (inputEvent is InputEventMouseMotion mouseMotion)
            UpdateHoverFromScreen(mouseMotion.Position);

        if (inputEvent.IsActionPressed("click_to_move") && inputEvent is InputEventMouseButton mouseButton)
        {
            if (_targetSelectionHandler?.IsTargetSelectionActive == true)
            {
                _targetSelectionHandler.TrySelectTargetFromScreen(mouseButton.Position);
                GetViewport().SetInputAsHandled();
                return;
            }
            UpdateHoverFromScreen(mouseButton.Position);
            TrySelectHoveredCell();
            GetViewport().SetInputAsHandled();
        }
    }

    public bool EnterCombat()
    {
        if (IsCombatActive)
            return true;
        if (!_grid.IsBuilt && !_grid.BuildGrid())
            return false;

        _player.CancelAutoMovement();
        _gameModeManager.SetCombatMode();
        _engagedUnits.Clear();

        if (_turnManager is null)
        {
            if (!TryEnterUnit(_primaryUnit))
            {
                _gameModeManager.SetExplorationMode();
                return false;
            }
        }
        else
        {
            List<TacticalUnit> startingUnits = GetTree()
                .GetNodesInGroup("tactical_units")
                .OfType<TacticalUnit>()
                .Where(unit => unit.StartAsParticipant)
                .ToList();
            foreach (TacticalUnit unit in startingUnits)
            {
                unit.UseTurnEconomy = true;
                if (TryEnterUnit(unit))
                    continue;
                RollBackCombatEntry();
                return false;
            }
            _turnManager.StartCombat(_engagedUnits);
        }

        IsCombatActive = true;
        LastPathLength = 0;
        LastPathCost = 0;
        _grid.ClearPath();
        _grid.Visible = true;
        TacticalUnit? commandedUnit = GetCommandedUnit();
        _grid.SetCurrentCell(commandedUnit?.CurrentCell);
        RefreshReachableCells();
        return true;
    }

    public void ExitCombat()
    {
        if (!IsCombatActive)
            return;

        _turnManager?.EndCombat();
        foreach (TacticalUnit unit in _engagedUnits.ToArray())
            unit.ExitCombat();
        _engagedUnits.Clear();
        _grid.ClearPath();
        _grid.ClearReachableCells();
        _grid.ClearHoveredCell();
        _grid.SetCurrentCell(null);
        _grid.Visible = false;
        LastPathLength = 0;
        LastPathCost = 0;
        IsCombatActive = false;
        _gameModeManager.SetExplorationMode();
    }

    public bool RequestEndTurn()
    {
        if (!IsCombatActive || _turnManager is null)
            return false;
        if (_movementActionHandler?.CanEndTurn(_turnManager.ActiveUnit) == false)
            return false;
        _grid.ClearPath();
        return _turnManager.EndCurrentTurn();
    }

    public bool SpendActiveActionPoint()
    {
        TacticalUnit? active = _turnManager?.ActiveUnit;
        return active is not null && active.SpendActionPoints(1);
    }

    public bool ModifyNextUnitInitiative(int delta)
    {
        if (_turnManager is null)
            return false;
        TacticalUnit? target = _turnManager.GetNextEligibleUnit();
        if (target is null)
            return false;
        _turnManager.ModifyInitiative(target, delta);
        return true;
    }

    public bool AddUnitToCombat(TacticalUnit unit)
    {
        if (!IsCombatActive || _turnManager is null || unit.IsCombatActive)
            return false;
        unit.UseTurnEconomy = true;
        unit.SetActorVisible(true);
        if (!TryEnterUnit(unit))
        {
            unit.SetActorVisible(false);
            return false;
        }
        if (_turnManager.AddParticipant(unit))
        {
            RefreshReachableCells();
            return true;
        }
        unit.ExitCombat();
        _engagedUnits.Remove(unit);
        return false;
    }

    public bool RemoveUnitFromCombat(TacticalUnit unit)
    {
        if (!IsCombatActive || _turnManager is null || !_engagedUnits.Contains(unit))
            return false;
        bool removed = _turnManager.RemoveParticipant(unit);
        unit.ExitCombat();
        unit.SetActorVisible(false);
        _engagedUnits.Remove(unit);
        RefreshReachableCells();
        return removed;
    }

    public bool TrySelectHoveredCell()
    {
        int hoveredId = _grid.GetHoveredCellId();
        return hoveredId >= 0 && TrySelectDestinationCellId(hoveredId);
    }

    public bool TrySelectDestinationWorld(Vector3 worldPosition)
    {
        TacticalCell? destination = _grid.GetNearestCell(worldPosition, _grid.CellSize * 0.9f);
        return destination is not null && TrySelectDestination(destination);
    }

    public bool TrySelectDestinationCellId(int cellId)
    {
        TacticalCell? destination = _grid.GetCellById(cellId);
        return destination is not null && TrySelectDestination(destination);
    }

    public bool SetHoveredCellFromWorld(Vector3 worldPosition)
    {
        return IsCombatActive && _grid.SetHoveredCellFromWorld(worldPosition);
    }

    public void RefreshReachableCells()
    {
        TacticalUnit? unit = GetCommandedUnit();
        if (_turnManager is null || unit is null || unit.CurrentCell is null || !unit.IsActiveTurn)
        {
            _grid.ClearReachableCells();
            return;
        }
        Dictionary<TacticalCell, int> reachable = TacticalPathfinder.FindReachableCells(
            unit.CurrentCell,
            unit.CurrentMovementPoints,
            unit.GetMovementCost);
        _grid.ShowReachableCells(reachable.Keys);
    }

    public bool GetIsCombatActive() => IsCombatActive;
    public int GetLastPathLength() => LastPathLength;
    public int GetLastPathCost() => LastPathCost;
    public string GetActiveUnitName() => GetCommandedUnit()?.UnitDisplayName ?? string.Empty;

    private bool TrySelectDestination(TacticalCell destination)
    {
        TacticalUnit? unit = GetCommandedUnit();
        if (!IsCombatActive || unit?.CurrentCell is null || unit.IsMoving)
            return false;
        if (_movementActionHandler?.CanStartMovement(unit, destination) == false)
            return false;
        if (unit.UseTurnEconomy && (!unit.IsActiveTurn || !_grid.GetCellIsReachable(destination.CellId)))
            return false;
        if (destination.IsOccupied && destination != unit.CurrentCell)
            return false;

        List<TacticalCell> path = unit.UseTurnEconomy
            ? TacticalPathfinder.FindPath(unit.CurrentCell, destination, (from, to) => unit.GetMovementCost(from, to))
            : TacticalPathfinder.FindPath(unit.CurrentCell, destination);
        LastPathLength = path.Count;
        LastPathCost = unit.GetPathMovementCost(path);
        if (path.Count == 0 || (unit.UseTurnEconomy && LastPathCost > unit.CurrentMovementPoints))
        {
            _grid.ClearPath();
            return false;
        }

        _grid.ShowPath(path);
        if (path.Count == 1 && _movementActionHandler is not null)
        {
            // FollowPath completes a zero-step path synchronously. Register the
            // action first so PathCompleted closes the existing movement context.
            _movementActionHandler.OnMovementStarted(unit, destination);
            return unit.FollowPath(path);
        }
        if (!unit.FollowPath(path))
            return false;
        _movementActionHandler?.OnMovementStarted(unit, destination);
        return true;
    }

    private bool TryEnterUnit(TacticalUnit unit)
    {
        TacticalCell? startCell = GetNearestAvailableCell(
            unit.Actor.GlobalPosition - Vector3.Up * unit.PlayerCenterHeight,
            MaximumEntrySnapDistance);
        if (startCell is null)
        {
            GD.PushWarning($"CombatModeController: aucune cellule libre assez proche pour {unit.UnitDisplayName}.");
            return false;
        }
        SubscribeUnit(unit);
        if (!unit.EnterCombat(startCell))
            return false;
        _engagedUnits.Add(unit);
        return true;
    }

    private TacticalCell? GetNearestAvailableCell(Vector3 position, float maximumDistance)
    {
        TacticalCell? nearest = null;
        float bestDistance = maximumDistance;
        foreach (TacticalCell cell in _grid.Cells)
        {
            if (cell.IsOccupied)
                continue;
            float distance = cell.WorldPosition.DistanceTo(position);
            if (distance >= bestDistance)
                continue;
            nearest = cell;
            bestDistance = distance;
        }
        return nearest;
    }

    private TacticalUnit? GetCommandedUnit()
    {
        return _turnManager?.ActiveUnit ?? _primaryUnit;
    }

    private void UpdateHoverFromScreen(Vector2 screenPosition)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screenPosition);
        Vector3 end = origin + _camera.ProjectRayNormal(screenPosition) * MouseRayLength;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end, 1);
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        Godot.Collections.Dictionary hit = _grid.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || !_grid.SetHoveredCellFromWorld(hit["position"].AsVector3()))
            _grid.ClearHoveredCell();
    }

    private void SubscribeUnit(TacticalUnit unit)
    {
        if (!_subscribedUnits.Add(unit))
            return;
        unit.CellReached += OnCellReached;
        unit.PathCompleted += OnPathCompleted;
        unit.ResourcesChanged += OnResourcesChanged;
    }

    private void UnsubscribeUnit(TacticalUnit unit)
    {
        if (!_subscribedUnits.Remove(unit))
            return;
        unit.CellReached -= OnCellReached;
        unit.PathCompleted -= OnPathCompleted;
        unit.ResourcesChanged -= OnResourcesChanged;
    }

    private void RollBackCombatEntry()
    {
        foreach (TacticalUnit unit in _engagedUnits.ToArray())
            unit.ExitCombat();
        _engagedUnits.Clear();
        _gameModeManager.SetExplorationMode();
    }

    private void OnCellReached(TacticalCell cell)
    {
        TacticalUnit? unit = GetCommandedUnit();
        if (unit?.CurrentCell == cell)
            _grid.SetCurrentCell(cell);
        if (unit is not null && _movementActionHandler?.OnMovementCellReached(unit, cell) == true)
        {
            unit.CancelPath();
            _grid.ClearPath();
        }
        RefreshReachableCells();
    }

    private void OnPathCompleted()
    {
        _grid.ClearPath();
        TacticalUnit? unit = GetCommandedUnit();
        if (unit is not null)
            _movementActionHandler?.OnMovementPathCompleted(unit);
        RefreshReachableCells();
    }

    private void OnResourcesChanged(TacticalUnit unit)
    {
        if (unit == GetCommandedUnit())
            RefreshReachableCells();
    }

    private void OnTurnStarted(TacticalUnit unit)
    {
        _grid.ClearPath();
        _grid.SetCurrentCell(unit.CurrentCell);
        RefreshReachableCells();
    }

    private void OnOrderChanged()
    {
        if (_turnManager?.ActiveUnit is not null)
            _grid.SetCurrentCell(_turnManager.ActiveUnit.CurrentCell);
    }
}
