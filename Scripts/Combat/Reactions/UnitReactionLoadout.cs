using System.Collections.Generic;
using Godot;

namespace MankindRenewal.Combat.Reactions;

public partial class UnitReactionLoadout : Node, IReactionProvider
{
    [Export] public NodePath OwnerUnitPath { get; set; } = new();
    [Export] public Godot.Collections.Array<ReactionDefinition> Definitions { get; set; } = new();

    public TacticalUnit OwnerUnit { get; private set; } = null!;
    private readonly List<ReactionDefinition> _validDefinitions = new();

    public override void _Ready()
    {
        OwnerUnit = GetNode<TacticalUnit>(OwnerUnitPath);
        foreach (ReactionDefinition definition in Definitions)
        {
            if (definition?.IsValidDefinition() == true)
                _validDefinitions.Add(definition);
            else
                GD.PushWarning($"UnitReactionLoadout: reaction invalide ignoree sur {OwnerUnit.UnitDisplayName}.");
        }
        AddToGroup("reaction_providers");
    }

    public IReadOnlyList<ReactionDefinition> GetReactionDefinitions() => _validDefinitions;
    public int GetReactionCount() => _validDefinitions.Count;
    public string GetReactionIdAt(int index) => index >= 0 && index < _validDefinitions.Count ? _validDefinitions[index].ReactionId : string.Empty;
}
