using Darkages.Sprites;

namespace Darkages.Enums;

public static class ItemEnumConverters
{
    public static string PaneToString(Item.ItemPanes e)
    {
        return e switch
        {
            Item.ItemPanes.Ground => "Ground",
            Item.ItemPanes.Inventory => "Inventory",
            Item.ItemPanes.Equip => "Equip",
            Item.ItemPanes.Bank => "Bank",
            Item.ItemPanes.Archived => "Archived",
            _ => "Ground"
        };
    }

    public static string ArmorVarianceToString(Item.Variance e)
    {
        return e switch
        {
            Item.Variance.None => "None",
            Item.Variance.Embunement => "Embunement",
            Item.Variance.Blessing => "Blessing",
            Item.Variance.Mana => "Mana",
            Item.Variance.Gramail => "Gramail",
            Item.Variance.Deoch => "Deoch",
            Item.Variance.Ceannlaidir => "Ceannlaidir",
            Item.Variance.Cail => "Cail",
            Item.Variance.Fiosachd => "Fiosachd",
            Item.Variance.Glioca => "Glioca",
            Item.Variance.Luathas => "Luathas",
            Item.Variance.Sgrios => "Sgrios",
            Item.Variance.Reinforcement => "Reinforcement",
            Item.Variance.Spikes => "Spikes",
            _ => "None"
        };
    }
}