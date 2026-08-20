using MankindRenewal.Items;

namespace MankindRenewal.Combat.Weapons;

public interface IActiveWeaponInstanceProvider : IActiveWeaponProvider
{
    ItemInstance? GetActiveWeaponInstance();
}
