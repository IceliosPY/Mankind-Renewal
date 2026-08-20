namespace MankindRenewal.Combat.Actions;

public enum AttackActionPhase
{
    Created,
    Declared,
    AwaitingReaction,
    CostCommitted,
    Launched,
    Resolving,
    Completed,
    Cancelled,
}
