using MankindRenewal.Combat;

namespace MankindRenewal.Equipment;

public interface IEquipmentOperationLock
{
    bool IsEquipmentOperationLocked(TacticalUnit owner);
}
