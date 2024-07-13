#region

#endregion

namespace Darkages.CommandSystem.Loot.Interfaces;

public interface ILootDropper
{
    ILootDefinition Drop(ILootTable lootTable);

    ILootDefinition Drop(ILootTable lootTable, string name);

    IEnumerable<ILootDefinition> Drop(ILootTable lootTable, int amount);

    IEnumerable<ILootDefinition> Drop(ILootTable lootTable, int amount, string name);
}