using System;
using System.Collections.Generic;
using Godot;
using MankindRenewal.Combat;
using MankindRenewal.Combat.Weapons;
using MankindRenewal.Items;

namespace MankindRenewal.Equipment;

public partial class EquipmentLoadout : Node, IActiveWeaponInstanceProvider
{
    [Export] public NodePath InventoryPath { get; set; } = new();
    [Export] public NodePath TacticalUnitPath { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentSlotDefinition> SlotDefinitions { get; set; } = new();

    public Inventory Inventory { get; private set; } = null!;
    public TacticalUnit OwnerUnit { get; private set; } = null!;
    public string ActiveSlotId { get; private set; } = string.Empty;

    public event Action<EquipmentLoadout>? EquipmentChanged;
    public event Action<WeaponDefinition?>? ActiveWeaponChanged;

    private readonly Dictionary<string, ItemInstance?> _equipped = new(StringComparer.Ordinal);
    private readonly List<EquipmentSlotDefinition> _orderedSlots = new();

    public override void _Ready()
    {
        Inventory = GetNode<Inventory>(InventoryPath);
        OwnerUnit = GetNode<TacticalUnit>(TacticalUnitPath);
        foreach (EquipmentSlotDefinition definition in SlotDefinitions)
        {
            if (definition is null || !definition.IsValidDefinition() || _equipped.ContainsKey(definition.SlotId))
            {
                GD.PushWarning($"EquipmentLoadout: slot invalide ou duplique sur {Name}.");
                continue;
            }
            _orderedSlots.Add(definition);
            _equipped.Add(definition.SlotId, null);
        }
        Inventory.RegisterRemovalGuard(CanRemoveFromInventory);
        AddToGroup("equipment_loadouts");
    }

    public override void _ExitTree()
    {
        if (Inventory is not null)
            Inventory.UnregisterRemovalGuard(CanRemoveFromInventory);
    }

    public bool Equip(ItemInstance? instance, string slotId)
    {
        if (OperationsLocked())
            return false;
        if (instance is null || !Inventory.Contains(instance) || !_equipped.ContainsKey(slotId))
            return false;
        EquipmentSlotDefinition? slot = GetSlotDefinition(slotId);
        if (slot is null || !IsCompatible(instance, slot))
            return false;
        string existingSlot = GetEquippedSlotId(instance);
        if (!string.IsNullOrEmpty(existingSlot) && existingSlot != slotId)
            return false;
        if (ReferenceEquals(_equipped[slotId], instance))
            return true;

        WeaponDefinition? previousActiveWeapon = GetActiveWeapon();
        _equipped[slotId] = instance;
        if (string.IsNullOrEmpty(ActiveSlotId))
            ActiveSlotId = slotId;
        NotifyChanges(previousActiveWeapon);
        return true;
    }

    public bool Unequip(string slotId)
    {
        if (OperationsLocked())
            return false;
        if (!_equipped.TryGetValue(slotId, out ItemInstance? instance) || instance is null)
            return false;
        WeaponDefinition? previousActiveWeapon = GetActiveWeapon();
        _equipped[slotId] = null;
        if (ActiveSlotId == slotId)
            ActiveSlotId = FindFirstOccupiedWeaponSlot();
        NotifyChanges(previousActiveWeapon);
        return true;
    }

    public bool SetActiveSlot(string slotId)
    {
        if (OperationsLocked())
            return false;
        if (!_equipped.TryGetValue(slotId, out ItemInstance? item)
            || item?.Definition is not WeaponDefinition
            || GetSlotDefinition(slotId)?.SlotType != EquipmentSlotType.Weapon)
            return false;
        if (ActiveSlotId == slotId)
            return true;
        WeaponDefinition? previousActiveWeapon = GetActiveWeapon();
        ActiveSlotId = slotId;
        NotifyChanges(previousActiveWeapon);
        return true;
    }

    public WeaponDefinition? GetActiveWeapon()
    {
        return _equipped.TryGetValue(ActiveSlotId, out ItemInstance? item)
            ? item?.Definition as WeaponDefinition
            : null;
    }

    public ItemInstance? GetActiveWeaponInstance()
    {
        return _equipped.TryGetValue(ActiveSlotId, out ItemInstance? item) ? item : null;
    }

    public ItemInstance? GetEquippedItem(string slotId)
    {
        return _equipped.TryGetValue(slotId, out ItemInstance? item) ? item : null;
    }

    public bool IsEquipped(ItemInstance? instance) => !string.IsNullOrEmpty(GetEquippedSlotId(instance));

    public string GetEquippedSlotId(ItemInstance? instance)
    {
        if (instance is null)
            return string.Empty;
        foreach ((string slotId, ItemInstance? equippedItem) in _equipped)
        {
            if (ReferenceEquals(equippedItem, instance))
                return slotId;
        }
        return string.Empty;
    }

    public int GetSlotCount() => _orderedSlots.Count;
    public string GetSlotIdAt(int index) => index >= 0 && index < _orderedSlots.Count ? _orderedSlots[index].SlotId : string.Empty;
    public string GetSlotDisplayNameAt(int index) => index >= 0 && index < _orderedSlots.Count ? _orderedSlots[index].DisplayName : string.Empty;
    public string GetEquippedInstanceId(string slotId) => GetEquippedItem(slotId)?.InstanceId ?? string.Empty;
    public string GetEquippedItemName(string slotId) => GetEquippedItem(slotId)?.Definition?.DisplayName ?? string.Empty;
    public string GetActiveSlotId() => ActiveSlotId;
    public string GetActiveWeaponName() => GetActiveWeapon()?.DisplayName ?? string.Empty;
    public bool GetHasActiveWeapon() => GetActiveWeapon() is not null;

    private EquipmentSlotDefinition? GetSlotDefinition(string slotId)
    {
        foreach (EquipmentSlotDefinition definition in _orderedSlots)
        {
            if (definition.SlotId == slotId)
                return definition;
        }
        return null;
    }

    private bool IsCompatible(ItemInstance instance, EquipmentSlotDefinition slot)
    {
        return slot.SlotType switch
        {
            EquipmentSlotType.Weapon => instance.Definition is WeaponDefinition,
            _ => false,
        };
    }

    private bool CanRemoveFromInventory(ItemInstance instance) => !OperationsLocked() && !IsEquipped(instance);

    private bool OperationsLocked()
    {
        foreach (Node node in GetTree().GetNodesInGroup("equipment_operation_locks"))
        {
            if (node is IEquipmentOperationLock operationLock && operationLock.IsEquipmentOperationLocked(OwnerUnit))
                return true;
        }
        return false;
    }

    private string FindFirstOccupiedWeaponSlot()
    {
        foreach (EquipmentSlotDefinition slot in _orderedSlots)
        {
            if (slot.SlotType == EquipmentSlotType.Weapon && _equipped[slot.SlotId]?.Definition is WeaponDefinition)
                return slot.SlotId;
        }
        return string.Empty;
    }

    private void NotifyChanges(WeaponDefinition? previousActiveWeapon)
    {
        EquipmentChanged?.Invoke(this);
        WeaponDefinition? activeWeapon = GetActiveWeapon();
        if (!ReferenceEquals(previousActiveWeapon, activeWeapon))
            ActiveWeaponChanged?.Invoke(activeWeapon);
    }
}
