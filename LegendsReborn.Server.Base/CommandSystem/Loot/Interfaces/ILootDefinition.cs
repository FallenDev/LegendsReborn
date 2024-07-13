namespace Darkages.CommandSystem.Loot.Interfaces;

public interface ILootDefinition : IWeighable
{
    string Name { get; set; }
}