using System.Collections.Generic;

namespace MankindRenewal.Combat.Damage;

public sealed class DamageResolutionResult
{
    public TacticalUnit Target { get; init; } = null!;
    public List<DamageComponentResult> Components { get; } = new();
    public double DecimalTotalDamage { get; internal set; }
    public int FinalDamage { get; internal set; }
    public int HpBefore { get; internal set; }
    public int HpAfter { get; internal set; }
    public bool TargetNeutralized { get; internal set; }
}
