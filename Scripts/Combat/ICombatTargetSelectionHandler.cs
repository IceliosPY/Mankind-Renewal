using Godot;

namespace MankindRenewal.Combat;

public interface ICombatTargetSelectionHandler
{
    bool IsTargetSelectionActive { get; }
    bool TrySelectTargetFromScreen(Vector2 screenPosition);
}
