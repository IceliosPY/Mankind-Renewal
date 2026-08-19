using Godot;

namespace MankindRenewal.Characters;

public partial class ClickToMoveSource : NavigationAgent3D
{
    [Export(PropertyHint.Range, "0.05,2.0,0.05")]
    public float MaximumProjectionDistance { get; set; } = 0.8f;

    private CharacterBody3D _characterBody = null!;
    private bool _hasDestination;
    private bool _stoppedAtDestination;

    public override void _Ready()
    {
        _characterBody = GetParent<CharacterBody3D>();
    }

    public bool TrySetDestination(Vector3 requestedPosition)
    {
        Rid navigationMap = GetNavigationMap();
        if (!navigationMap.IsValid || NavigationServer3D.MapGetIterationId(navigationMap) == 0)
            return false;

        Vector3 navigablePosition = NavigationServer3D.MapGetClosestPoint(navigationMap, requestedPosition);
        if (navigablePosition.DistanceTo(requestedPosition) > MaximumProjectionDistance)
            return false;

        TargetPosition = navigablePosition - Vector3.Up * PathHeightOffset;
        _hasDestination = true;
        _stoppedAtDestination = false;
        return true;
    }

    public Vector3 GetWorldDirection(Vector3 currentPosition)
    {
        if (!_hasDestination)
            return Vector3.Zero;

        if (IsNavigationFinished())
        {
            CompleteDestination();
            return Vector3.Zero;
        }

        Vector3 direction = GetNextPathPosition() - currentPosition;
        direction.Y = 0.0f;
        return direction.Normalized();
    }

    public void Cancel()
    {
        _hasDestination = false;
        _stoppedAtDestination = false;
        TargetPosition = _characterBody.GlobalPosition;
    }

    public bool HasActiveDestination() => _hasDestination;

    public bool ConsumeDestinationStop()
    {
        bool shouldStop = _stoppedAtDestination;
        _stoppedAtDestination = false;
        return shouldStop;
    }

    private void CompleteDestination()
    {
        _hasDestination = false;
        _stoppedAtDestination = true;
        TargetPosition = _characterBody.GlobalPosition;
    }
}
