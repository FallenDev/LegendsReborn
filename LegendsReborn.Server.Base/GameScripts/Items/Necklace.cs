﻿using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Sprites;

namespace Darkages.GameScripts.Items;

[Script("Necklace")]
public class Necklace(Item item) : ItemScript(item)
{
    public override void OnUse(Sprite sprite, byte slot)
    {
        if (sprite == null) return;
        if (Item?.Template == null) return;
        if (sprite is not Aisling aisling) return;
        var client = aisling.Client;
        if (!Item.Template.Flags.FlagIsSet(ItemFlags.Equipable)) return;

        if (client.CheckReqs(client, Item))
            client.Aisling.EquipmentManager.Add(Item.Template.EquipmentSlot, Item);
    }

    public override void Equipped(Sprite sprite, byte displaySlot)
    {
        if (sprite == null) return;
        if (Item?.Template == null) return;
        if (sprite is not Aisling aisling) return;
        var client = aisling.Client;
        if (!Item.Template.Flags.FlagIsSet(ItemFlags.Equipable)) return;

        if (Item.Template.Flags.FlagIsSet(ItemFlags.Elemental))
            sprite.OffenseElement = Item.Template.OffenseElement;

        CalculateGearPoints(client);
    }

    public override void UnEquipped(Sprite sprite, byte displaySlot)
    {
        if (sprite == null) return;
        if (Item?.Template == null) return;
        if (sprite is not Aisling aisling) return;
        var client = aisling.Client;
        if (!Item.Template.Flags.FlagIsSet(ItemFlags.Equipable)) return;

        if (Item.Template.Flags.FlagIsSet(ItemFlags.Elemental))
            sprite.OffenseElement = ElementManager.Element.None;
        
        CalculateGearPoints(client);
    }
}