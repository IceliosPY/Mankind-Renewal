using Godot;
using MankindRenewal.Systems;

namespace MankindRenewal.Characters;

public partial class PlayerController : CharacterBody3D
{
    [ExportGroup("Movement")]
    [Export(PropertyHint.Range, "0.1,20.0,0.1")]
    public float MoveSpeed { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0.1,50.0,0.1")]
    public float GroundAcceleration { get; set; } = 25.0f;

    [Export(PropertyHint.Range, "0.1,50.0,0.1")]
    public float AirAcceleration { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0.1,20.0,0.1")]
    public float JumpVelocity { get; set; } = 5.0f;

    [ExportGroup("Click To Move")]
    [Export(PropertyHint.Range, "10.0,5000.0,10.0")]
    public float ClickRayLength { get; set; } = 1000.0f;

    private ManualMovementSource _manualMovement = null!;
    private ClickToMoveSource _clickToMove = null!;
    private GameModeManager? _gameModeManager;
    private float _gravity;

    public override void _Ready()
    {
        _manualMovement = GetNode<ManualMovementSource>("ManualMovement");
        _clickToMove = GetNode<ClickToMoveSource>("ClickToMove");
        _gameModeManager = GetTree().GetFirstNodeInGroup("game_mode_manager") as GameModeManager;
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity").AsDouble();

        if (_gameModeManager is not null)
            _gameModeManager.ModeChanged += OnGameModeChanged;
    }

    public override void _ExitTree()
    {
        if (_gameModeManager is not null)
            _gameModeManager.ModeChanged -= OnGameModeChanged;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!IsExplorationActive() || _manualMovement.HasInput())
            return;

        if (inputEvent.IsActionPressed("click_to_move") && inputEvent is InputEventMouseButton mouseButton)
            TrySetClickDestinationFromScreen(mouseButton.Position);
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaSeconds = (float)delta;
        Vector3 velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= _gravity * deltaSeconds;
        else if (IsExplorationActive() && Input.IsActionJustPressed("jump"))
            velocity.Y = JumpVelocity;

        Vector3 movementDirection = Vector3.Zero;
        if (IsExplorationActive())
        {
            Camera3D? camera = GetViewport().GetCamera3D();
            if (_manualMovement.HasInput())
            {
                _clickToMove.Cancel();
                if (camera is not null)
                    movementDirection = _manualMovement.GetWorldDirection(camera.GlobalBasis);
            }
            else
            {
                movementDirection = _clickToMove.GetWorldDirection(GlobalPosition);
            }
        }
        else
        {
            _clickToMove.Cancel();
        }

        Vector3 targetHorizontalVelocity = movementDirection * MoveSpeed;
        float acceleration = IsOnFloor() ? GroundAcceleration : AirAcceleration;
        if (_clickToMove.ConsumeDestinationStop())
        {
            velocity.X = 0.0f;
            velocity.Z = 0.0f;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, targetHorizontalVelocity.X, acceleration * deltaSeconds);
            velocity.Z = Mathf.MoveToward(velocity.Z, targetHorizontalVelocity.Z, acceleration * deltaSeconds);
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    public bool TrySetClickDestination(Vector3 worldPosition)
    {
        if (!IsExplorationActive() || _manualMovement.HasInput())
            return false;

        return _clickToMove.TrySetDestination(worldPosition);
    }

    public void CancelAutoMovement()
    {
        _clickToMove.Cancel();
    }

    public bool GetIsAutoMoving() => _clickToMove.HasActiveDestination();

    public bool TrySetClickDestinationFromScreen(Vector2 screenPosition)
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null)
            return false;

        Vector3 rayOrigin = camera.ProjectRayOrigin(screenPosition);
        Vector3 rayEnd = rayOrigin + camera.ProjectRayNormal(screenPosition) * ClickRayLength;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd, collisionMask: 1);
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0 && TrySetClickDestination(hit["position"].AsVector3());
    }

    private bool IsExplorationActive()
    {
        return _gameModeManager is null || _gameModeManager.IsExploration();
    }

    private void OnGameModeChanged(GameMode mode)
    {
        if (mode != GameMode.Exploration)
            _clickToMove.Cancel();
    }
}
