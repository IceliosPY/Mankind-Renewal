using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

public sealed class DamageComponentResult
{
    public DamageType DamageType { get; init; }
    public double RawDamage { get; init; }
    public double CoverMultiplier { get; init; } = 1.0;
    public double DamageBeforeResistance { get; init; }
    public double BaseResistance { get; init; }
    public double PenetrationApplied { get; init; }
    public double EffectiveResistance { get; init; }
    public double ReductionPercentage { get; init; }
    public double DamageAfterResistance { get; init; }
}
