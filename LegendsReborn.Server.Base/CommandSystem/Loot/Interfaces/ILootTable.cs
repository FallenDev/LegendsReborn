#region

#endregion

namespace Darkages.CommandSystem.Loot.Interfaces;

public interface ILootTable : ILootDefinition
{
    ICollection<ILootDefinition> Children { get; }

    ILootTable Add(ILootDefinition item);

    ILootDefinition Get(string name);

    ILootTable Remove(ILootDefinition item);
}