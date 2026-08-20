namespace MankindRenewal.Combat;

public interface ICombatMovementActionHandler
{
    bool CanStartMovement(TacticalUnit unit, TacticalCell destination);
    void OnMovementStarted(TacticalUnit unit, TacticalCell destination);
    bool OnMovementCellReached(TacticalUnit unit, TacticalCell cell);
    void OnMovementPathCompleted(TacticalUnit unit);
    bool CanEndTurn(TacticalUnit? activeUnit);
}
