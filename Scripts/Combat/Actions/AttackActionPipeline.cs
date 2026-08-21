using System;
using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Actions;

public sealed class AttackActionPipeline
{
    private readonly TurnManager _turnManager;
    private readonly Action<TacticalUnit> _neutralizeUnit;
    private readonly ICombatAttackRules? _attackRules;

    public AttackActionPipeline(TurnManager turnManager, Action<TacticalUnit> neutralizeUnit, ICombatAttackRules? attackRules = null)
    {
        _turnManager = turnManager;
        _neutralizeUnit = neutralizeUnit;
        _attackRules = attackRules;
    }

    public AttackValidationStatus Validate(TacticalUnit? attacker, TacticalUnit? target, WeaponDefinition? weapon)
    {
        if (attacker is null || _turnManager.ActiveUnit != attacker || !attacker.IsActiveTurn || attacker.IsNeutralized)
            return AttackValidationStatus.NoActiveUnit;
        if (weapon is null || !weapon.IsValidDefinition())
            return AttackValidationStatus.NoWeapon;
        if (target is null || target == attacker)
            return AttackValidationStatus.InvalidTarget;
        if (target.IsNeutralized)
            return AttackValidationStatus.NeutralizedTarget;
        if (!target.IsCombatActive)
            return AttackValidationStatus.InvalidTarget;
        if (target.TeamId == attacker.TeamId)
            return AttackValidationStatus.AlliedTarget;
        if (attacker.IsMoving)
            return AttackValidationStatus.AttackerBusy;
        int distance = GetTacticalDistance(attacker, target);
        if (distance < 1 || distance > weapon.RangeInCells)
            return AttackValidationStatus.OutOfRange;
        if (_attackRules?.EvaluateAttack(attacker, target, weapon).IsBlocked == true)
            return AttackValidationStatus.LineOfFireBlocked;
        if (attacker.CurrentActionPoints < weapon.ActionPointCost)
            return AttackValidationStatus.InsufficientActionPoints;
        return AttackValidationStatus.Valid;
    }

    public CombatAttackAction? Declare(TacticalUnit attacker, TacticalUnit target, WeaponDefinition weapon)
    {
        if (Validate(attacker, target, weapon) != AttackValidationStatus.Valid)
            return null;

        CombatAttackAction action = new(attacker, target, weapon, GetTacticalDistance(attacker, target))
        {
            Phase = AttackActionPhase.Declared,
            OfferedReaction = GetAvailableReaction(target, weapon),
        };

        if (action.OfferedReaction == DefensiveReactionType.None)
            ResolveAfterReaction(action, false);
        else
            action.Phase = AttackActionPhase.AwaitingReaction;
        return action;
    }

    public CombatAttackAction? Declare(CombatActionContext context)
    {
        if (context.Kind != CombatActionKind.NormalAttack || context.Target is null || context.Weapon is null)
            return null;
        if (Validate(context.Source, context.Target, context.Weapon) != AttackValidationStatus.Valid)
        {
            context.WasCancelledBeforeLaunch = true;
            return null;
        }

        CombatAttackAction action = new(
            context.ActionId,
            context.Source,
            context.Target,
            context.WeaponInstance,
            context.Weapon,
            GetTacticalDistance(context.Source, context.Target))
        {
            Phase = AttackActionPhase.Declared,
            OfferedReaction = GetAvailableReaction(context.Target, context.Weapon),
        };
        action.Evaluation = Evaluate(context.Source, context.Target, context.Weapon);
        context.AttackEvaluation = action.Evaluation;
        if (action.Evaluation.Interceptor is not null)
            action.OfferedReaction = DefensiveReactionType.None;

        if (action.OfferedReaction == DefensiveReactionType.None)
            ResolveAfterReaction(action, false);
        else
            action.Phase = AttackActionPhase.AwaitingReaction;
        SyncContext(context, action);
        return action;
    }

    public bool ResolveReaction(CombatActionContext context, CombatAttackAction action, bool acceptReaction)
    {
        bool resolved = ResolveReaction(action, acceptReaction);
        if (resolved)
            SyncContext(context, action);
        return resolved;
    }

    public bool ResolveImmediateReactionAttack(CombatActionContext context)
    {
        if (!context.IsReaction || context.CanTriggerReactions || context.Target is null || context.Weapon is null)
            return false;
        TacticalUnit source = context.Source;
        TacticalUnit target = context.Target;
        if (!source.IsCombatActive || source.IsNeutralized || !target.IsCombatActive || target.IsNeutralized || source.TeamId == target.TeamId)
            return false;
        int distance = GetTacticalDistance(source, target);
        if (distance < 1 || distance > context.Weapon.RangeInCells)
            return false;

        CombatAttackEvaluation evaluation = Evaluate(source, target, context.Weapon);
        context.AttackEvaluation = evaluation;
        if (evaluation.IsBlocked)
        {
            context.WasCancelledBeforeLaunch = true;
            return false;
        }

        context.WasLaunched = true;
        context.AppliedDamage = ApplyWeaponDamage(evaluation.ResolvedTarget, context.Weapon, evaluation.DamageMultiplier);
        return true;
    }

    public bool ResolveReaction(CombatAttackAction action, bool acceptReaction)
    {
        if (action.Phase != AttackActionPhase.AwaitingReaction || action.ReactionConsumed)
            return false;
        action.ReactionConsumed = true;
        action.ReactionWasRefused = !acceptReaction;
        action.ChosenReaction = acceptReaction ? action.OfferedReaction : DefensiveReactionType.None;
        ResolveAfterReaction(action, acceptReaction);
        return true;
    }

    public static int GetTacticalDistance(TacticalUnit attacker, TacticalUnit target)
    {
        if (attacker.CurrentCell is null || target.CurrentCell is null)
            return int.MaxValue;
        int horizontal = Math.Abs(attacker.CurrentCell.GridX - target.CurrentCell.GridX)
            + Math.Abs(attacker.CurrentCell.GridZ - target.CurrentCell.GridZ);
        float heightDifference = Mathf.Abs(attacker.CurrentCell.SurfaceHeight - target.CurrentCell.SurfaceHeight);
        int vertical = Mathf.CeilToInt(heightDifference / 2.0f);
        return horizontal + vertical;
    }

    private void ResolveAfterReaction(CombatAttackAction action, bool useReaction)
    {
        if (Validate(action.Attacker, action.Target, action.Weapon) != AttackValidationStatus.Valid)
        {
            action.Outcome = AttackOutcome.CancelledBeforeLaunch;
            action.Phase = AttackActionPhase.Cancelled;
            return;
        }

        CombatAttackEvaluation evaluation = Evaluate(action.Attacker, action.Target, action.Weapon);
        action.Evaluation = evaluation;
        if (evaluation.IsBlocked)
        {
            action.Outcome = AttackOutcome.CancelledBeforeLaunch;
            action.Phase = AttackActionPhase.Cancelled;
            return;
        }

        if (!action.Attacker.SpendActionPoints(action.Weapon.ActionPointCost))
        {
            action.Outcome = AttackOutcome.CancelledBeforeLaunch;
            action.Phase = AttackActionPhase.Cancelled;
            return;
        }

        action.WasCostCommitted = true;
        action.Phase = AttackActionPhase.CostCommitted;
        action.WasLaunched = true;
        action.Phase = AttackActionPhase.Launched;
        action.Phase = AttackActionPhase.Resolving;

        if (useReaction && DefenceBeatsAccuracy(action))
        {
            action.Outcome = action.ChosenReaction == DefensiveReactionType.Dodge
                ? AttackOutcome.Dodged
                : AttackOutcome.Parried;
        }
        else
        {
            action.Outcome = AttackOutcome.Hit;
            action.AppliedDamage = ApplyWeaponDamage(evaluation.ResolvedTarget, action.Weapon, evaluation.DamageMultiplier);
        }
        action.Phase = AttackActionPhase.Completed;
    }

    private static DefensiveReactionType GetAvailableReaction(TacticalUnit target, WeaponDefinition weapon)
    {
        if (weapon.AttackType == WeaponAttackType.Ranged)
            return DefensiveReactionType.Dodge;
        return target.GetActiveWeapon()?.AllowsParry == true
            ? DefensiveReactionType.Parry
            : DefensiveReactionType.None;
    }

    private static bool DefenceBeatsAccuracy(CombatAttackAction action)
    {
        int defence = action.ChosenReaction switch
        {
            DefensiveReactionType.Dodge => action.Target.GetEffectiveDodge(),
            DefensiveReactionType.Parry => action.Target.GetEffectiveParry(),
            _ => 0,
        };
        // Strict comparison is intentional: the attacker wins every equality.
        int accuracy = action.Evaluation?.EffectiveAccuracy ?? action.Weapon.BaseAccuracy;
        return defence > Mathf.Max(accuracy, 0);
    }

    private int ApplyWeaponDamage(TacticalUnit target, WeaponDefinition weapon, float multiplier = 1.0f)
    {
        int damage = target.ApplyRawDamage(weapon.GetRawDamage() * Mathf.Max(multiplier, 0.0f));
        if (target.IsNeutralized)
            _neutralizeUnit(target);
        return damage;
    }

    private static void SyncContext(CombatActionContext context, CombatAttackAction action)
    {
        context.WasLaunched = action.WasLaunched;
        context.WasCostCommitted = action.WasCostCommitted;
        context.AppliedDamage = action.AppliedDamage;
        context.WasCancelledBeforeLaunch = action.Outcome == AttackOutcome.CancelledBeforeLaunch;
        context.AttackEvaluation = action.Evaluation;
    }

    private CombatAttackEvaluation Evaluate(TacticalUnit attacker, TacticalUnit target, WeaponDefinition weapon)
        => _attackRules?.EvaluateAttack(attacker, target, weapon) ?? CombatAttackEvaluation.Open(target, weapon);
}
