using Godot;
using MankindRenewal.Equipment;

namespace MankindRenewal.Tests;

public partial class CombatPrototype04Setup : Node
{
    [Export] public NodePath InventorySetupPath { get; set; } = new();

    public override void _Ready()
    {
        InventoryEquipmentPrototypeSetup setup = GetNode<InventoryEquipmentPrototypeSetup>(InventorySetupPath);
        EquipmentLoadout? unitB = setup.GetLoadout("UNITE B");
        EquipmentLoadout? unitC = setup.GetLoadout("UNITE C");
        EquipmentLoadout? unitD = setup.GetLoadout("UNITE D");
        if (unitB is null || unitC is null || unitD is null
            || !unitB.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            || !unitC.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId)
            || !unitD.SetActiveSlot(InventoryEquipmentPrototypeSetup.SecondaryWeaponSlotId))
        {
            GD.PushError("CombatPrototype04Setup: configuration d'armes V4 incomplete.");
        }
    }
}
