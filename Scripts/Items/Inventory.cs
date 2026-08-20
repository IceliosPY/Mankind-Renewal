using System;
using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Items;

public partial class Inventory : Node
{
    public IReadOnlyList<ItemInstance> Items => _items;

    public event Action<ItemInstance>? ItemAdded;
    public event Action<ItemInstance>? ItemRemoved;
    public event Action? InventoryChanged;

    private readonly List<ItemInstance> _items = new();
    private readonly Dictionary<string, ItemInstance> _itemsById = new(StringComparer.Ordinal);
    private readonly List<Func<ItemInstance, bool>> _removalGuards = new();

    public bool AddItem(ItemInstance? instance)
    {
        if (instance?.Definition is null || instance.OwnerInventory is not null)
            return false;
        instance.EnsureInstanceId();
        if (!instance.IsValidInstance() || _itemsById.ContainsKey(instance.InstanceId))
            return false;
        _items.Add(instance);
        _itemsById.Add(instance.InstanceId, instance);
        instance.OwnerInventory = this;
        ItemAdded?.Invoke(instance);
        InventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemInstance? instance)
    {
        if (instance is null || !Contains(instance))
            return false;
        foreach (Func<ItemInstance, bool> guard in _removalGuards)
        {
            if (!guard(instance))
                return false;
        }
        _items.Remove(instance);
        _itemsById.Remove(instance.InstanceId);
        instance.OwnerInventory = null;
        ItemRemoved?.Invoke(instance);
        InventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItemById(string instanceId) => RemoveItem(FindByInstanceId(instanceId));

    public ItemInstance? FindByInstanceId(string instanceId)
    {
        return _itemsById.TryGetValue(instanceId, out ItemInstance? item) ? item : null;
    }

    public bool Contains(ItemInstance? instance)
    {
        return instance is not null
            && _itemsById.TryGetValue(instance.InstanceId, out ItemInstance? owned)
            && ReferenceEquals(owned, instance);
    }

    internal void RegisterRemovalGuard(Func<ItemInstance, bool> guard)
    {
        if (!_removalGuards.Contains(guard))
            _removalGuards.Add(guard);
    }

    internal void UnregisterRemovalGuard(Func<ItemInstance, bool> guard) => _removalGuards.Remove(guard);

    public int GetItemCount() => _items.Count;
    public ItemInstance? GetItemAt(int index) => index >= 0 && index < _items.Count ? _items[index] : null;
    public bool HasInstanceId(string instanceId) => _itemsById.ContainsKey(instanceId);
    public bool HasItem(ItemInstance? instance) => Contains(instance);
}
