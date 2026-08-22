using MankindRenewal.Combat.Weapons;
using MankindRenewal.Combat.Damage;
using MankindRenewal.Items;

namespace MankindRenewal.Combat.Actions;

public sealed class CombatActionContext
{
    public long ActionId { get; init; } = CombatActionId.Next();
    public CombatActionKind Kind { get; init; }
    public TacticalUnit Source { get; init; } = null!;
    public TacticalUnit? Target { get; init; }
    public ItemInstance? WeaponInstance { get; init; }
    public WeaponDefinition? Weapon { get; init; }
    public int PlannedActionPointCost { get; init; }
    public bool IsReaction => Kind == CombatActionKind.ReactionAttack;
    public bool IsExplicitFreeAction => Kind == CombatActionKind.ExplicitFreeAction;
    public bool CanTriggerReactions { get; init; } = true;
    public bool MovementTriggersEnabled { get; init; } = true;
    public int DestinationCellId { get; set; } = -1;
    public int AppliedDamage { get; internal set; }
    public bool WasLaunched { get; internal set; }
    public bool WasCostCommitted { get; internal set; }
    public bool WasCancelledBeforeLaunch { get; internal set; }
    public CombatAttackEvaluation? AttackEvaluation { get; internal set; }
    public DamageResolutionResult? DamageResolution { get; internal set; }

    public static CombatActionContext NormalAttack(
        TacticalUnit source,
        TacticalUnit target,
        ItemInstance? weaponInstance,
        WeaponDefinition weapon)
    {
        return new CombatActionContext
        {
            Kind = CombatActionKind.NormalAttack,
            Source = source,
            Target = target,
            WeaponInstance = weaponInstance,
            Weapon = weapon,
            PlannedActionPointCost = weapon.ActionPointCost,
        };
    }

    public static CombatActionContext NormalMovement(TacticalUnit source, int destinationCellId)
    {
        return new CombatActionContext
        {
            Kind = CombatActionKind.NormalMovement,
            Source = source,
            DestinationCellId = destinationCellId,
        };
    }
}
