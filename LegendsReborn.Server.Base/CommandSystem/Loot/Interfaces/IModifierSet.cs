#region

#endregion

namespace Darkages.CommandSystem.Loot.Interfaces;

public interface IModifierSet : ILootDefinition
{
    ICollection<IModifier> Modifiers { get; }

    IModifierSet Add(IModifier modifier);

    void ModifyItem(object item);

    IModifierSet Remove(IModifier modifier);
}