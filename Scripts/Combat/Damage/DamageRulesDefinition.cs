using Godot;

namespace MankindRenewal.Combat.Damage;

[GlobalClass]
public partial class DamageRulesDefinition : Resource
{
    public const float AbsoluteMaximumResistanceReduction = 0.20f;

    [Export(PropertyHint.Range, "0,0.2,0.005")]
    public float MaxResistanceReduction { get; set; } = 0.20f;

    [Export(PropertyHint.Range, "0.1,1000,0.1")]
    public float ResistanceScale { get; set; } = 30.0f;

    public float GetMaxResistanceReduction()
        => Mathf.Clamp(MaxResistanceReduction, 0.0f, AbsoluteMaximumResistanceReduction);

    public float GetResistanceScale() => Mathf.Max(ResistanceScale, 0.0001f);
}
