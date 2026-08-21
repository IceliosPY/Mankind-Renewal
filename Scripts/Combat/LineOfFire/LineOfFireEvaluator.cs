using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Combat.Cover;

namespace MankindRenewal.Combat.LineOfFire;

public sealed class LineOfFireEvaluator
{
    private readonly TacticalGrid _grid;
    private readonly CoverRulesDefinition _rules;
    private readonly Func<IEnumerable<CoverProvider3D>> _providers;
    private readonly Func<IEnumerable<TacticalUnit>> _units;

    public LineOfFireEvaluator(
        TacticalGrid grid,
        CoverRulesDefinition rules,
        Func<IEnumerable<CoverProvider3D>> providers,
        Func<IEnumerable<TacticalUnit>> units)
    {
        _grid = grid;
        _rules = rules;
        _providers = providers;
        _units = units;
    }

    public (CoverProvider3D? Provider, float Fraction) FindFirstObstruction(TacticalUnit source, TacticalUnit target)
    {
        Vector3 start = source.GetFireOriginWorldPosition();
        Vector3 end = target.GetTargetPointWorldPosition();
        return _providers()
            .Where(provider => provider.TacticalEnabled && provider.BlocksLineOfFire)
            .Select(provider =>
            {
                Vector3 point = provider.GetEdgeWorldPosition(_grid) + Vector3.Up * 0.9f;
                (float fraction, float distance) = DistanceToSegment(point, start, end);
                return (Provider: provider, Fraction: fraction, Distance: distance);
            })
            .Where(hit => hit.Fraction > 0.04f && hit.Fraction < 0.96f && hit.Distance <= hit.Provider.ObstructionRadius)
            .OrderBy(hit => hit.Fraction)
            .Select(hit => (hit.Provider, hit.Fraction))
            .FirstOrDefault();
    }

    public (TacticalUnit? Unit, float Fraction) FindFirstInterceptor(TacticalUnit source, TacticalUnit intendedTarget)
    {
        Vector3 start = source.GetFireOriginWorldPosition();
        Vector3 end = intendedTarget.GetTargetPointWorldPosition();
        return _units()
            .Where(unit => unit != source && unit != intendedTarget && unit.IsCombatActive && !unit.IsNeutralized)
            .Select(unit =>
            {
                (float fraction, float distance) = DistanceToSegment(unit.GetTargetPointWorldPosition(), start, end);
                return (Unit: unit, Fraction: fraction, Distance: distance);
            })
            .Where(hit => hit.Fraction > 0.08f && hit.Fraction < 0.92f && hit.Distance <= _rules.UnitInterceptionRadius)
            .OrderBy(hit => hit.Fraction)
            .Select(hit => (hit.Unit, hit.Fraction))
            .FirstOrDefault();
    }

    private static (float Fraction, float Distance) DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
            return (0.0f, point.DistanceTo(start));
        float fraction = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        Vector3 closest = start + segment * fraction;
        return (fraction, point.DistanceTo(closest));
    }
}
