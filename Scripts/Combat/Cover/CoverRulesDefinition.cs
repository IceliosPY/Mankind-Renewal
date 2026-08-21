using Godot;

namespace MankindRenewal.Combat.Cover;

[GlobalClass]
public partial class CoverRulesDefinition : Resource
{
    [Export(PropertyHint.Range, "0,100,1")] public int LightAccuracyPenalty { get; set; } = 5;
    [Export(PropertyHint.Range, "0,100,1")] public int HeavyAccuracyPenalty { get; set; } = 10;
    [Export(PropertyHint.Range, "0.5,10,0.5")] public float HeightLevelThreshold { get; set; } = 2.0f;
    [Export(PropertyHint.Range, "0,1,0.05")] public float CoverPiercingDamageMultiplier { get; set; } = 0.5f;
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float UnitInterceptionRadius { get; set; } = 0.7f;

    public int GetPenalty(CoverLevel level) => level switch
    {
        CoverLevel.Light => Mathf.Max(LightAccuracyPenalty, 0),
        CoverLevel.Heavy => Mathf.Max(HeavyAccuracyPenalty, 0),
        _ => 0,
    };

    public int GetLightAccuracyPenalty() => LightAccuracyPenalty;
    public int GetHeavyAccuracyPenalty() => HeavyAccuracyPenalty;
    public float GetHeightLevelThreshold() => HeightLevelThreshold;
    public float GetCoverPiercingDamageMultiplier() => CoverPiercingDamageMultiplier;
}
