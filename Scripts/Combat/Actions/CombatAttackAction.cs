using System.Threading;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Actions;

public sealed class CombatAttackAction
{
    private static long _nextId;

    public CombatAttackAction(TacticalUnit attacker, TacticalUnit target, WeaponDefinition weapon, int distanceInCells)
    {
        ActionId = Interlocked.Increment(ref _nextId);
        Attacker = attacker;
        Target = target;
        Weapon = weapon;
        DistanceInCells = distanceInCells;
    }

    public long ActionId { get; }
    public TacticalUnit Attacker { get; }
    public TacticalUnit Target { get; }
    public WeaponDefinition Weapon { get; }
    public int DistanceInCells { get; }
    public AttackActionPhase Phase { get; internal set; } = AttackActionPhase.Created;
    public DefensiveReactionType OfferedReaction { get; internal set; }
    public DefensiveReactionType ChosenReaction { get; internal set; }
    public bool ReactionWasRefused { get; internal set; }
    public bool ReactionConsumed { get; internal set; }
    public bool WasCostCommitted { get; internal set; }
    public bool WasLaunched { get; internal set; }
    public int AppliedDamage { get; internal set; }
    public AttackOutcome Outcome { get; internal set; } = AttackOutcome.Pending;
}
