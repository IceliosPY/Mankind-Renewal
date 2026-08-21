namespace MankindRenewal.Combat.Actions;

public enum AttackValidationStatus
{
    Valid,
    NoActiveUnit,
    NoWeapon,
    InvalidTarget,
    AlliedTarget,
    NeutralizedTarget,
    OutOfRange,
    InsufficientActionPoints,
    AttackerBusy,
    LineOfFireBlocked,
}
