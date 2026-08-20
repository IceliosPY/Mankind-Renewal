using System.Collections.Generic;

namespace MankindRenewal.Combat.Reactions;

public interface IReactionProvider
{
    TacticalUnit OwnerUnit { get; }
    IReadOnlyList<ReactionDefinition> GetReactionDefinitions();
}
