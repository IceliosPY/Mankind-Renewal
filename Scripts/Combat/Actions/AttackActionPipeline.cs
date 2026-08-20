using System;
using Godot;
using MankindRenewal.Combat.Weapons;

namespace MankindRenewal.Combat.Actions;

public sealed class AttackActionPipeline
{
    private readonly TurnManager _turnManager;
    private readonly Action<TacticalUnit> _neutralizeUnit;

    public AttackActionPipeline(TurnManager turnManager, Action<TacticalUnit> neutralizeUnit)
    {
        _turnManager = turnManager;
        _neutralizeUnit = neutralizeUnit;
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
            action.AppliedDamage = action.Target.ApplyRawDamage(action.Weapon.GetRawDamage());
            if (action.Target.IsNeutralized)
                _neutralizeUnit(action.Target);
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
        return defence > action.Attacker.GetEffectiveAccuracy();
    }
}
