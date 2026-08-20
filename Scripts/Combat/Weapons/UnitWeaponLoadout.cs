using Godot;

namespace MankindRenewal.Combat.Weapons;

// Temporary V3 adapter. A future EquipmentLoadout can implement the same provider contract.
public partial class UnitWeaponLoadout : Node, IActiveWeaponProvider
{
    [Export] public WeaponDefinition? ActiveWeapon { get; set; }

    public WeaponDefinition? GetActiveWeapon() => ActiveWeapon;
    public string GetActiveWeaponId() => ActiveWeapon?.ItemId ?? string.Empty;
    public string GetActiveWeaponName() => ActiveWeapon?.DisplayName ?? string.Empty;
}
