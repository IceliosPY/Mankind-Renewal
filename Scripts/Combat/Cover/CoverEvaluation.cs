using System.Collections.Generic;

namespace MankindRenewal.Combat.Cover;

public sealed class CoverEvaluation
{
    public CoverLevel BaseCover { get; init; }
    public CoverLevel EffectiveCover { get; init; }
    public int AccuracyPenalty { get; init; }
    public int HeightLevelsReduced { get; init; }
    public bool IsFlanked { get; init; }
    public IReadOnlyList<CoverDirection> AttackDirections { get; init; } = System.Array.Empty<CoverDirection>();
    public CoverProvider3D? Provider { get; init; }
}
