using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Actions;

public class CombatAttackEvaluation
{
    public TacticalUnit IntendedTarget { get; init; } = null!;
    public TacticalUnit ResolvedTarget { get; init; } = null!;
    public TacticalUnit? Interceptor { get; init; }
    public bool HasLineOfSight { get; init; } = true;
    public bool HasLineOfFire { get; init; } = true;
    public bool IsBlocked { get; init; }
    public bool UsesCoverPiercing { get; init; }
    public bool IsFriendlyFire { get; init; }
    public int EffectiveAccuracy { get; init; }
    public float DamageMultiplier { get; init; } = 1.0f;
    public string BlockReason { get; init; } = string.Empty;

    public static CombatAttackEvaluation Open(TacticalUnit target, WeaponDefinition weapon)
    {
        return new CombatAttackEvaluation
        {
            IntendedTarget = target,
            ResolvedTarget = target,
            EffectiveAccuracy = weapon.BaseAccuracy,
        };
    }
}
