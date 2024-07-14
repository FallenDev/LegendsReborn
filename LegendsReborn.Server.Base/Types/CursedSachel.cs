using Chaos.Common.Definitions;
using Darkages.Enums;
using Darkages.Sprites;
using MapFlags = Darkages.Enums.MapFlags;

namespace Darkages.Types;

public class CursedSachel
{
    public CursedSachel(Aisling parent)
    {
        Owner = parent;
        Items = new HashSet<Item>();
    }

    public ISet<Item> Items { get; set; }
    public Position Location { get; set; }
    public int MapId { get; set; }
    public Aisling Owner { get; set; }

    public void ReepItems(List<Item> items = null)
    {

        if (Owner == null)
            return;

        Items = items != null ? [..items] : new HashSet<Item>();
        {
            Location = new Position(Owner.XPos, Owner.YPos);
            MapId = Owner.CurrentMapId;
            if (!Owner.Client.Aisling.Map.Flags.HasFlag(MapFlags.Raid))
                Reap();

            Owner.Client.SystemMessage("Your items are torn from your body.");
            Owner.Client.SendAttributes(StatUpdateType.Full);
        }
    }

    private void Reap()
    {
        //equipment
        foreach(var gear in Owner.EquipmentManager.Equipment.Values.Where(gear => gear != null).ToList())
        {
            var item = gear.Item;

            item.Durability -= Math.Max(0, item.Template.MaxDurability / 10);

            if(item.Durability <= 0 && !item.Template.Flags.HasFlag(ItemFlags.Equipable))
            {
                Owner.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Your {item.Template.Name} shatters!");
                Owner.EquipmentManager.RemoveForDeath(gear.Slot, false);
                continue;
            }

            if (!item.Template.Flags.HasFlag(ItemFlags.Dropable) && !item.Template.Flags.HasFlag(ItemFlags.Perishable))
                continue;

            Owner.EquipmentManager.RemoveForDeath(gear.Slot, false);

            if (item.Template.Flags.HasFlag(ItemFlags.Perishable))
            {
                Owner.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Your {item.Template.Name} shatters!");
                continue;
            }

            item.Release(Owner, Owner.Position);
        }

        //gold
        var gold = Owner.GoldPoints;
        Money.Create(Owner, gold, Owner.Position);
        Owner.GoldPoints = 0;

        //inventory
        foreach(var item in Owner.Inventory.Snapshot())
        {
            item.Durability -= Math.Max(0, item.Template.MaxDurability / 10);

            if (item.Durability <= 0 && item.Template.Flags.HasFlag(ItemFlags.Equipable))
            {
                Owner.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Your {item.Template.Name} shatters!");
                Owner.Inventory.Remove(item.InventorySlot);
                continue;
            }

            if (!item.Template.Flags.HasFlag(ItemFlags.Dropable) && !item.Template.Flags.HasFlag(ItemFlags.Perishable))
                continue;

            Owner.Inventory.Remove(item.InventorySlot);

            if (item.Template.Flags.HasFlag(ItemFlags.Perishable))
            {
                Owner.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"Your {item.Template.Name} shatters!");
                continue;
            }

            item.Release(Owner, Owner.Position);
        }
    }
}