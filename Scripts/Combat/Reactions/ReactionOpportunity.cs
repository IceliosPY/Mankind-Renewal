using System.Collections.Generic;
using MankindRenewal.Combat.Actions;

namespace MankindRenewal.Combat.Reactions;

public sealed class ReactionOpportunity
{
    public ReactionOpportunity(CombatActionContext sourceAction, TacticalUnit reactor, IReadOnlyList<ReactionDefinition> choices)
    {
        SourceAction = sourceAction;
        Reactor = reactor;
        Choices = choices;
    }

    public CombatActionContext SourceAction { get; }
    public TacticalUnit Reactor { get; }
    public IReadOnlyList<ReactionDefinition> Choices { get; }
}
