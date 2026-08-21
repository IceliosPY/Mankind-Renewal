using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Actions;

public interface ICombatAttackRules
{
    CombatAttackEvaluation EvaluateAttack(TacticalUnit attacker, TacticalUnit target, WeaponDefinition weapon);
}
