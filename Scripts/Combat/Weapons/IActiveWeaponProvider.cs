namespace MankindRenewal.Combat.Weapons;

public interface IActiveWeaponProvider
{
    WeaponDefinition? GetActiveWeapon();
}
