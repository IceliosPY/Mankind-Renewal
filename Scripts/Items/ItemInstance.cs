using System;
using Godot;

namespace MankindRenewal.Items;

[GlobalClass]
public partial class ItemInstance : Resource
{
    [ExportGroup("Instance identity")]
    [Export] public string InstanceId { get; set; } = string.Empty;
    [Export] public ItemDefinition? Definition { get; set; }

    internal Inventory? OwnerInventory { get; set; }

    public static ItemInstance Create(ItemDefinition definition)
    {
        ItemInstance instance = new() { Definition = definition };
        instance.EnsureInstanceId();
        return instance;
    }

    public void EnsureInstanceId()
    {
        if (string.IsNullOrWhiteSpace(InstanceId))
            InstanceId = Guid.NewGuid().ToString("N");
    }

    public bool IsValidInstance()
    {
        return !string.IsNullOrWhiteSpace(InstanceId) && Definition?.IsIdentityValid() == true;
    }

    public string GetItemInstanceId() => InstanceId;
    public string GetDefinitionId() => Definition?.DefinitionId ?? string.Empty;
    public string GetDisplayName() => Definition?.DisplayName ?? string.Empty;
    public ItemDefinition? GetDefinition() => Definition;
    public bool GetIsValidInstance() => IsValidInstance();
}
