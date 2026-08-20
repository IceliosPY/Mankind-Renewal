using System.Collections.Generic;
using System.Linq;
using Godot;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Equipment;
using MankindRenewal.Items;

namespace MankindRenewal.Tests;

public partial class InventoryEquipmentPrototypeSetup : Node
{
    public const string PrimaryWeaponSlotId = "weapon.primary";
    public const string SecondaryWeaponSlotId = "weapon.secondary";

    [Export] public WeaponDefinition? DebugPistol { get; set; }
    [Export] public WeaponDefinition? DebugBlade { get; set; }
    [Export] public WeaponDefinition? DebugRifle { get; set; }
    [Export] public WeaponDefinition? DebugHeavyPistol { get; set; }
    [Export] public ItemDefinition? DebugUtility { get; set; }

    public ItemInstance? UnitAPistolA { get; private set; }
    public ItemInstance? UnitAPistolB { get; private set; }
    public ItemInstance? UnitABlade { get; private set; }
    public ItemInstance? UnitADebugUtility { get; private set; }

    private readonly List<ItemDefinition> _testDefinitions = new();

    public override void _Ready()
    {
        foreach (ItemDefinition? definition in new ItemDefinition?[] { DebugPistol, DebugBlade, DebugRifle, DebugHeavyPistol, DebugUtility })
        {
            if (definition is not null)
                _testDefinitions.Add(definition);
        }

        EquipmentLoadout? unitA = GetLoadout("UNITE A");
        EquipmentLoadout? unitB = GetLoadout("UNITE B");
        EquipmentLoadout? unitC = GetLoadout("UNITE C");
        EquipmentLoadout? unitD = GetLoadout("UNITE D");
        if (unitA is null || unitB is null || unitC is null || unitD is null
            || DebugPistol is null || DebugBlade is null || DebugRifle is null || DebugHeavyPistol is null)
        {
            GD.PushError("InventoryEquipmentPrototypeSetup: configuration incomplete.");
            return;
        }

        UnitAPistolA = Add(unitA.Inventory, DebugPistol);
        UnitAPistolB = Add(unitA.Inventory, DebugPistol);
        UnitABlade = Add(unitA.Inventory, DebugBlade);
        Add(unitA.Inventory, DebugRifle);
        if (DebugUtility is not null)
            UnitADebugUtility = Add(unitA.Inventory, DebugUtility);
        unitA.Equip(UnitAPistolA, PrimaryWeaponSlotId);
        unitA.Equip(UnitABlade, SecondaryWeaponSlotId);
        unitA.SetActiveSlot(PrimaryWeaponSlotId);

        ItemInstance unitBBlade = Add(unitB.Inventory, DebugBlade);
        ItemInstance unitBHeavyPistol = Add(unitB.Inventory, DebugHeavyPistol);
        unitB.Equip(unitBBlade, PrimaryWeaponSlotId);
        unitB.Equip(unitBHeavyPistol, SecondaryWeaponSlotId);
        unitB.SetActiveSlot(PrimaryWeaponSlotId);

        ItemInstance unitCPistol = Add(unitC.Inventory, DebugPistol);
        ItemInstance unitCBlade = Add(unitC.Inventory, DebugBlade);
        unitC.Equip(unitCPistol, PrimaryWeaponSlotId);
        unitC.Equip(unitCBlade, SecondaryWeaponSlotId);
        unitC.SetActiveSlot(SecondaryWeaponSlotId);

        ItemInstance unitDRifle = Add(unitD.Inventory, DebugRifle);
        ItemInstance unitDHeavyPistol = Add(unitD.Inventory, DebugHeavyPistol);
        unitD.Equip(unitDRifle, PrimaryWeaponSlotId);
        unitD.Equip(unitDHeavyPistol, SecondaryWeaponSlotId);
        unitD.SetActiveSlot(PrimaryWeaponSlotId);
    }

    public EquipmentLoadout? GetLoadout(string unitDisplayName)
    {
        return GetTree().GetNodesInGroup("equipment_loadouts")
            .OfType<EquipmentLoadout>()
            .FirstOrDefault(loadout => loadout.OwnerUnit.UnitDisplayName == unitDisplayName);
    }

    public Inventory? GetInventory(string unitDisplayName) => GetLoadout(unitDisplayName)?.Inventory;

    public ItemInstance? CreateAndAdd(string unitDisplayName, string definitionId)
    {
        Inventory? inventory = GetInventory(unitDisplayName);
        ItemDefinition? definition = _testDefinitions.FirstOrDefault(item => item.DefinitionId == definitionId);
        if (inventory is null || definition is null)
            return null;
        ItemInstance instance = ItemInstance.Create(definition);
        return inventory.AddItem(instance) ? instance : null;
    }

    public ItemInstance? CreateLooseInstance(string definitionId)
    {
        ItemDefinition? definition = _testDefinitions.FirstOrDefault(item => item.DefinitionId == definitionId);
        return definition is null ? null : ItemInstance.Create(definition);
    }

    public string GetUnitAPistolAId() => UnitAPistolA?.InstanceId ?? string.Empty;
    public string GetUnitAPistolBId() => UnitAPistolB?.InstanceId ?? string.Empty;
    public string GetUnitABladeId() => UnitABlade?.InstanceId ?? string.Empty;
    public string GetUnitADebugUtilityId() => UnitADebugUtility?.InstanceId ?? string.Empty;

    private static ItemInstance Add(Inventory inventory, ItemDefinition definition)
    {
        ItemInstance instance = ItemInstance.Create(definition);
        if (!inventory.AddItem(instance))
            throw new System.InvalidOperationException($"Impossible d'ajouter {definition.DefinitionId} a l'inventaire de test.");
        return instance;
    }
}
