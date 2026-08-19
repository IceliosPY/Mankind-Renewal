using System.Collections.Generic;
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
    [Export(PropertyHint.Range, "0.5,5.0,0.1")] public float MaximumEntrySnapDistance { get; set; } = 2.2f;
    [Export] public float PlayerCenterHeight { get; set; } = 0.9f;
    [Export] public float MouseRayLength { get; set; } = 1000.0f;

    public bool IsCombatActive { get; private set; }
    public int LastPathLength { get; private set; }

    private TacticalGrid _grid = null!;
    private TacticalUnit _unit = null!;
    private PlayerController _player = null!;
    private GameModeManager _gameModeManager = null!;
    private Camera3D _camera = null!;

    public override void _Ready()
    {
        _grid = GetNode<TacticalGrid>(GridPath);
        _unit = GetNode<TacticalUnit>(TacticalUnitPath);
        _player = GetNode<PlayerController>(PlayerPath);
        _gameModeManager = GetNode<GameModeManager>(GameModeManagerPath);
        _camera = GetNode<Camera3D>(CameraPath);
        _unit.CellReached += OnCellReached;
        _unit.PathCompleted += OnPathCompleted;
    }

    public override void _ExitTree()
    {
        if (_unit is null)
            return;
        _unit.CellReached -= OnCellReached;
        _unit.PathCompleted -= OnPathCompleted;
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
        Vector3 playerFeet = _player.GlobalPosition - Vector3.Up * PlayerCenterHeight;
        TacticalCell? startCell = _grid.GetNearestCell(playerFeet, MaximumEntrySnapDistance);
        if (startCell is null)
        {
            GD.PushWarning("CombatModeController: aucune cellule assez proche pour entrer en combat.");
            return false;
        }

        _gameModeManager.SetCombatMode();
        if (!_unit.EnterCombat(startCell))
        {
            _gameModeManager.SetExplorationMode();
            return false;
        }

        IsCombatActive = true;
        LastPathLength = 0;
        _grid.SetCurrentCell(startCell);
        _grid.ClearPath();
        _grid.Visible = true;
        return true;
    }

    public void ExitCombat()
    {
        if (!IsCombatActive)
            return;

        _unit.ExitCombat();
        _grid.ClearPath();
        _grid.ClearHoveredCell();
        _grid.SetCurrentCell(null);
        _grid.Visible = false;
        LastPathLength = 0;
        IsCombatActive = false;
        _gameModeManager.SetExplorationMode();
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

    public bool GetIsCombatActive() => IsCombatActive;

    public int GetLastPathLength() => LastPathLength;

    private bool TrySelectDestination(TacticalCell destination)
    {
        if (!IsCombatActive || _unit.CurrentCell is null || _unit.IsMoving)
            return false;

        List<TacticalCell> path = TacticalPathfinder.FindPath(_unit.CurrentCell, destination);
        LastPathLength = path.Count;
        if (path.Count == 0)
        {
            _grid.ClearPath();
            return false;
        }

        _grid.ShowPath(path);
        return _unit.FollowPath(path);
    }

    private void UpdateHoverFromScreen(Vector2 screenPosition)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screenPosition);
        Vector3 end = origin + _camera.ProjectRayNormal(screenPosition) * MouseRayLength;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, end, 1);
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
        Godot.Collections.Dictionary hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || !_grid.SetHoveredCellFromWorld(hit["position"].AsVector3()))
            _grid.ClearHoveredCell();
    }

    private void OnCellReached(TacticalCell cell)
    {
        _grid.SetCurrentCell(cell);
    }

    private void OnPathCompleted()
    {
        _grid.ClearPath();
    }
}
