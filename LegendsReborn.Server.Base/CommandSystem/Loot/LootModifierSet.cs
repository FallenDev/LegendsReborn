#region

using Darkages.CommandSystem.Loot.Interfaces;
using Darkages.Templates;

#endregion

namespace Darkages.CommandSystem.Loot;

public class LootModifierSet : Template, IModifierSet
{
    public LootModifierSet(string name, int weight)
    {
        Name = name;
        Weight = weight;
        Modifiers = new List<IModifier>();
    }

    public ICollection<IModifier> Modifiers { get; }
    public decimal Weight { get; set; }

    public IModifierSet Add(IModifier modifier)
    {
        Modifiers.Add(modifier);
        return this;
    }

    public override string[] GetMetaData() =>
    [
        ""
    ];

    public void ModifyItem(object item)
    {
        if (Modifiers.Count == 0)
            return;

        foreach (var modifier in Modifiers)
            modifier.Apply(item);
    }

    public IModifierSet Remove(IModifier modifier)
    {
        Modifiers.Remove(modifier);
        return this;
    }

    public override string ToString() => $"Name: {Name}, Weight: {Weight}, Modifier Count: {Modifiers.Count}";
}