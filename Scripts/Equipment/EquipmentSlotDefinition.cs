using Godot;

namespace MankindRenewal.Equipment;

[GlobalClass]
public partial class EquipmentSlotDefinition : Resource
{
    [Export] public string SlotId { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = "Equipment Slot";
    [Export] public EquipmentSlotType SlotType { get; set; } = EquipmentSlotType.Weapon;

    public bool IsValidDefinition() => !string.IsNullOrWhiteSpace(SlotId) && !string.IsNullOrWhiteSpace(DisplayName);
}
