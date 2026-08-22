using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Damage;

public interface IDamageResolver
{
    DamageResolutionResult ResolveAndApply(TacticalUnit target, WeaponDefinition weapon, float coverMultiplier);
}
