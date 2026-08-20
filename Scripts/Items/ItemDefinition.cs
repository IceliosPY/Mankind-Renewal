using Godot;

namespace MankindRenewal.Items;

[GlobalClass]
public partial class ItemDefinition : Resource
{
    [ExportGroup("Identity")]
    [Export] public string ItemId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = "Unnamed item";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

    public string DefinitionId => ItemId;

    public bool IsIdentityValid() => !string.IsNullOrWhiteSpace(ItemId) && !string.IsNullOrWhiteSpace(DisplayName);
    public string GetDefinitionId() => DefinitionId;
}
