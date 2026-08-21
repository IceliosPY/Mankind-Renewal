using System;
using System.Collections.Generic;
using System.Linq;

namespace MankindRenewal.Combat.Cover;

public sealed class CoverEvaluator
{
    private readonly TacticalGrid _grid;
    private readonly CoverRulesDefinition _rules;
    private readonly Func<IEnumerable<CoverProvider3D>> _providers;

    public CoverEvaluator(TacticalGrid grid, CoverRulesDefinition rules, Func<IEnumerable<CoverProvider3D>> providers)
    {
        _grid = grid;
        _rules = rules;
        _providers = providers;
    }

    public CoverEvaluation Evaluate(TacticalUnit attacker, TacticalUnit target)
    {
        if (attacker.CurrentCell is null || target.CurrentCell is null)
            return new CoverEvaluation();

        IReadOnlyList<CoverDirection> directions = GetAttackDirections(attacker.CurrentCell, target.CurrentCell);
        List<CoverProvider3D> local = _providers()
            .Where(provider => provider.TacticalEnabled && provider.ResolveProtectedCell(_grid) == target.CurrentCell)
            .ToList();
        CoverProvider3D? selected = local
            .Where(provider => directions.Contains(provider.ProtectedDirection))
            .OrderByDescending(provider => provider.Level)
            .FirstOrDefault();
        CoverLevel baseCover = selected?.Level ?? CoverLevel.None;
        bool reduceForHeight = attacker.CurrentCell.SurfaceHeight - target.CurrentCell.SurfaceHeight >= _rules.HeightLevelThreshold;
        int reduced = reduceForHeight && baseCover is CoverLevel.Light or CoverLevel.Heavy ? 1 : 0;
        CoverLevel effective = reduced == 0 ? baseCover : baseCover == CoverLevel.Heavy ? CoverLevel.Light : CoverLevel.None;
        bool flanked = baseCover == CoverLevel.None && local.Any(provider => provider.Level != CoverLevel.None);
        return new CoverEvaluation
        {
            BaseCover = baseCover,
            EffectiveCover = effective,
            AccuracyPenalty = _rules.GetPenalty(effective),
            HeightLevelsReduced = reduced,
            IsFlanked = flanked,
            AttackDirections = directions,
            Provider = selected,
        };
    }

    public CoverLevel ApplyHeightForTest(CoverLevel level, float attackerHeight, float targetHeight)
    {
        if (attackerHeight - targetHeight < _rules.HeightLevelThreshold || level is CoverLevel.None or CoverLevel.Total)
            return level;
        return level == CoverLevel.Heavy ? CoverLevel.Light : CoverLevel.None;
    }

    public static IReadOnlyList<CoverDirection> GetAttackDirections(TacticalCell attacker, TacticalCell target)
    {
        int deltaX = attacker.GridX - target.GridX;
        int deltaZ = attacker.GridZ - target.GridZ;
        int x = Math.Abs(deltaX);
        int z = Math.Abs(deltaZ);
        if (x == z && x > 0)
        {
            return new[]
            {
                deltaX > 0 ? CoverDirection.East : CoverDirection.West,
                deltaZ > 0 ? CoverDirection.South : CoverDirection.North,
            };
        }
        if (x > z)
            return new[] { deltaX > 0 ? CoverDirection.East : CoverDirection.West };
        return new[] { deltaZ > 0 ? CoverDirection.South : CoverDirection.North };
    }
}
