using System.Threading;

namespace MankindRenewal.Combat.Actions;

public static class CombatActionId
{
    private static long _nextId;

    public static long Next() => Interlocked.Increment(ref _nextId);
}
