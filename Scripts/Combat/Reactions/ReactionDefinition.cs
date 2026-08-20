using Godot;

namespace MankindRenewal.Combat.Reactions;

[GlobalClass]
public partial class ReactionDefinition : Resource
{
    [ExportGroup("Identity")]
    [Export] public string ReactionId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string DebugDescription { get; set; } = string.Empty;

    [ExportGroup("Trigger and conditions")]
    [Export] public ReactionTriggerType Trigger { get; set; }
    [Export] public ReactionWeaponRequirement WeaponRequirement { get; set; }
    [Export(PropertyHint.Range, "0,100,1")] public int MaximumRangeInCells { get; set; }

    [ExportGroup("Resolution")]
    [Export] public ReactionResolutionType Resolution { get; set; } = ReactionResolutionType.ImmediateActiveWeaponAttack;
    [Export] public bool AllowsReactionChains { get; set; }

    [ExportGroup("Limits and future cost extension")]
    [Export(PropertyHint.Range, "0,100,1")] public int MaximumUsesPerRound { get; set; }
    [Export(PropertyHint.Range, "0,100,1")] public int ActionPointCost { get; set; }
    [Export(PropertyHint.Range, "0,100,1")] public int MovementPointCost { get; set; }
    [Export] public string SpecialResourceId { get; set; } = string.Empty;
    [Export(PropertyHint.Range, "0,100,1")] public int SpecialResourceCost { get; set; }

    public bool IsValidDefinition()
    {
        return !string.IsNullOrWhiteSpace(ReactionId)
            && !string.IsNullOrWhiteSpace(DisplayName)
            && MaximumRangeInCells >= 0
            && MaximumUsesPerRound >= 0
            && ActionPointCost >= 0
            && MovementPointCost >= 0
            && SpecialResourceCost >= 0;
    }

    public string GetReactionId() => ReactionId;
    public string GetDisplayName() => DisplayName;
    public int GetTriggerValue() => (int)Trigger;
    public int GetMaximumUsesPerRound() => MaximumUsesPerRound;
    public bool GetIsValidDefinition() => IsValidDefinition();
}
