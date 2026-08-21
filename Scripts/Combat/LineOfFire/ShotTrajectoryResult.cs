using MankindRenewal.Combat.Actions;
using MankindRenewal.Combat.Cover;

namespace MankindRenewal.Combat.LineOfFire;

public sealed class ShotTrajectoryResult : CombatAttackEvaluation
{
    public CoverEvaluation Cover { get; init; } = new();
    public CoverProvider3D? BlockingProvider { get; init; }
    public float InterceptorPathFraction { get; init; } = 1.0f;
}
