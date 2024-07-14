using Chaos.Common.Definitions;
using Darkages.Enums;
using Darkages.Network.Client;
using Darkages.Sprites;
using Darkages.Types;
using Dapper;
using Darkages.Object;
using EquipmentSlot = Darkages.Models.EquipmentSlot;
using Darkages.Database;
using Microsoft.Data.SqlClient;

namespace Darkages.Managers;

public class EquipmentManager
{
    public EquipmentManager(WorldClient client)
    {
        Client = client;
        Equipment = [];

        for (byte i = 1; i < 19; i++)
            Equipment[i] = null;
    }

    public WorldClient Client { get; set; }
    public int Length => Equipment?.Count ?? 0;
    public Dictionary<int, EquipmentSlot> Equipment { get; set; }

    public EquipmentSlot Weapon => Equipment[ItemSlots.Weapon];
    public EquipmentSlot Armor => Equipment[ItemSlots.Armor];
    public EquipmentSlot Shield => Equipment[ItemSlots.Shield];
    public EquipmentSlot Helmet => Equipment[ItemSlots.Helmet];
    public EquipmentSlot Earring => Equipment[ItemSlots.Earring];
    public EquipmentSlot Necklace => Equipment[ItemSlots.Necklace];
    public EquipmentSlot LHand => Equipment[ItemSlots.LHand];
    public EquipmentSlot RHand => Equipment[ItemSlots.RHand];
    public EquipmentSlot LArm => Equipment[ItemSlots.LArm];
    public EquipmentSlot RArm => Equipment[ItemSlots.RArm];
    public EquipmentSlot Waist => Equipment[ItemSlots.Waist];
    public EquipmentSlot Leg => Equipment[ItemSlots.Leg];
    public EquipmentSlot Foot => Equipment[ItemSlots.Foot];
    public EquipmentSlot FirstAcc => Equipment[ItemSlots.FirstAcc];
    public EquipmentSlot OverCoat => Equipment[ItemSlots.OverCoat];
    public EquipmentSlot OverHelm => Equipment[ItemSlots.OverHelm];
    public EquipmentSlot SecondAcc => Equipment[ItemSlots.SecondAcc];
    public EquipmentSlot ThirdAcc => Equipment[ItemSlots.ThirdAcc];

    public void Add(int displayslot, Item item)
    {
        if (Client == null) 
            return;
        if ((displayslot <= 0) || (displayslot > 17)) 
            return;
        if (item?.Template == null) 
            return;
        if (!item.Template.Flags.HasFlag(ItemFlags.Equipable)) 
            return;

        Equipment ??= new Dictionary<int, EquipmentSlot>();

        HandleEquipmentSwap(displayslot, item);
        Client.Aisling.CurrentWeight += item.Template.CarryWeight;
        Client.Aisling.LastEquipOrUnEquip = DateTime.UtcNow;

    }

    private void AddEquipment(int displayslot, Item item, bool remove = true)
    {
        Equipment[displayslot] = new EquipmentSlot(displayslot, item);

        if (remove)
            Client.Aisling.Inventory.Remove(item.InventorySlot);

        AddToAislingDb(Client.Aisling, item, displayslot);
        DisplayToEquipment((byte)displayslot, item);
        OnEquipmentAdded((byte)displayslot);
    }

    public void DecreaseDurability()
    {
        foreach (var item in Equipment.Values.Select(equipment => equipment?.Item)
                     .Where(item => item?.Template != null))
        {
            if (item.Template.Flags.HasFlag(ItemFlags.Equipable) && (item.Template.EquipmentSlot is not 14 or 15))
                item.Durability--;

            ManageDurabilitySignals(item);
        }
    }

    private void DisplayToEquipment(byte displayslot, Item item)
    {
        if (item != null)
            Client.SendEquipment(displayslot, item);
    }
        
    public bool RemoveForDeath(int displaySlot, bool returnIt = true)
    {
        if (Equipment[displaySlot] == null)
            return true;

        var itemObj = Equipment[displaySlot].Item;

        if (itemObj == null)
            return false;

        DeleteFromAislingDb(itemObj);
        RemoveFromSlot(displaySlot);

        if (!returnIt)
            return HandleDeathPile(itemObj);

        return Client.Aisling.GiveItem(itemObj) || HandleDeathPile(itemObj);
    }
    public bool RemoveFromExisting(int displayslot, bool returnit = true)
    {
        if (Equipment[displayslot] == null) 
            return true;

        var itemObj = Equipment[displayslot].Item;

        if (itemObj == null)
            return false;
            
        DeleteFromAislingDb(itemObj);
        RemoveFromSlot(displayslot);

        if (!returnit)
            return HandleUnreturnedItem(itemObj);

        return Client.Aisling.GiveItem(itemObj) || HandleUnreturnedItem(itemObj);
    }
    public void HandleEquipmentSwap(int displaySlot, Item item, bool returnIt = true)
    {
        Item itemObj = null;

        if (Equipment[displaySlot] != null)
        {
            itemObj = Equipment[displaySlot].Item;
            DeleteFromAislingDb(itemObj);
        }

        if (item == null)
            return;

        RemoveFromSlot(displaySlot);
        AddEquipment(displaySlot, item);

        if (!returnIt)
            HandleUnreturnedItem(itemObj);

        Client.Aisling.GiveItem(itemObj);
    }
    private bool HandleDeathPile(Item itemObj)
    {
        if (itemObj == null)
            return true;

        if (Client.Aisling.CurrentWeight < 0 || Client.Aisling.CurrentWeight > 500)
            Client.Aisling.CurrentWeight = 0;
        ObjectManager.DelObject(itemObj);
        Client.SendAttributes(StatUpdateType.Primary);

        return true;
    }
    private bool HandleUnreturnedItem(Item itemObj)
    {
        if (itemObj == null)
            return true;
            
        if (Client.Aisling.CurrentWeight < 0 || Client.Aisling.CurrentWeight > 500)
            Client.Aisling.CurrentWeight = 0;

        if (itemObj.Durability is not 0)
        {
            Client.SendServerMessage(ServerMessageType.OrangeBar2, $"{itemObj.Template.Name} was sent to storage.");
            Client.Aisling.Bank.Deposit(itemObj);
        }
        ObjectManager.DelObject(itemObj);
        Client.SendAttributes(StatUpdateType.Primary);

        return true;
    }
    private void ManageDurabilitySignals(Item item)
    {
        item.Durability = Math.Clamp(item.Durability, 0, item.Template.MaxDurability);
            
        var durablityPct = Math.Abs(item.Durability * 100 / item.Template.MaxDurability);
            
        if ((item.Durability is 0) && item.Template.Flags.HasFlag(ItemFlags.Equipable))
        {
            Client.SystemMessage($"{{=c {item.Template.Name} has been destroyed!");
            Client.Aisling.EquipmentManager.RemoveFromExisting(item.Template.EquipmentSlot, false);
            return;
        }
        if (durablityPct <= 1 && !item.Warnings[0])
        {
            Client.SystemMessage($"{{=c {item.Template.Name} is almost broken! (< 1%)");
            item.Warnings[0] = true;
        }
        if ((durablityPct <= 10) && !item.Warnings[1])
        {
            Client.SystemMessage($"{{=c {item.Template.Name} is almost worn out! (< 10%)");
            item.Warnings[1] = true;
        }
        else if ((durablityPct <= 30) && (durablityPct > 10) && !item.Warnings[2])
        {
            Client.SystemMessage($"{{=c{item.Template.Name} is wearing out. (< 30%)");
            item.Warnings[2] = true;
        }
        else if ((durablityPct <= 50) && (durablityPct > 30) && !item.Warnings[3])
        {
            Client.SystemMessage($"{{=c{item.Template.Name} has been damaged. (< 50%)");
            item.Warnings[3] = true;
        }
    }
    private void OnEquipmentAdded(byte displayslot)
    {
        var scripts = Equipment[displayslot].Item?.Scripts;
        if (scripts != null)
        {
            var scriptsValues = scripts.Values;
            foreach (var script in scriptsValues)
                script.Equipped(Client.Aisling, displayslot);
        }

        var item = Equipment[displayslot].Item;

        if (item != null)
            item.Equipped = true;

        Client.SendAttributes(StatUpdateType.Full);
        Client.UpdateDisplay();
    }
    private void OnEquipmentRemoved(byte displayslot)
    {
        if (Equipment[displayslot] == null)
            return;

        var item = Equipment[displayslot].Item;
        var itemScripts = item?.Scripts;
        if (itemScripts != null)
        {
            var scripts = itemScripts.Values;
            foreach (var script in scripts)
                script.UnEquipped(Client.Aisling, displayslot);
        }

        if (item != null)
        {
            item.Equipped = false;
            Client.Aisling.CurrentWeight -= item.Template.CarryWeight;
        }

        Client.SendAttributes(StatUpdateType.Full);
        Client.UpdateDisplay();
    }
    private void RemoveFromSlot(int displayslot)
    {
        OnEquipmentRemoved((byte)displayslot);
        Client.SendUnequip((Chaos.Common.Definitions.EquipmentSlot)displayslot);
        Equipment[displayslot] = null;
    }
    public static void AddToAislingDb(Sprite aisling, Item item, int slot)
    {
        try
        {
            var sConn = new SqlConnection(AislingStorage.ConnectionString);
            var adapter = new SqlDataAdapter();
            var s = item.Template.Name.Replace("'", "''");
            sConn.Open();
            var color = ItemColors.ItemColorsToInt(item.Template.Color);
            var playerInventory = "INSERT INTO LegendsPlayers.dbo.PlayersEquipped (ItemId, Name, Serial, Slot, Color, Image, DisplayImage, Durability, MaxDurability, Owner, InventorySlot, Stacks, Enchantable)" +
                                  $"VALUES ('{item.ItemId}','{s}','{aisling.Serial}','{slot}','{color}','{item.Image}','{item.DisplayImage}','{item.Durability}','{item.Template.MaxDurability}','{item.Owner}','{item.InventorySlot}','{item.Stacks}', '{item.Template.Enchantable}')";

            var cmd10 = new SqlCommand(playerInventory, sConn);
            adapter.InsertCommand = cmd10;
            adapter.InsertCommand.ExecuteNonQuery();

            sConn.Close();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
    }
    public static void DeleteFromAislingDb(Item item)
    {
        var sConn = new SqlConnection(AislingStorage.ConnectionString);
        if (item.ItemId == 0) 
            return;

        try
        {
            sConn.Open();

            const string cmd = "DELETE FROM LegendsPlayers.dbo.PlayersEquipped WHERE ItemId = @ItemId";
            sConn.Execute(cmd, new { item.ItemId });

            sConn.Close();
        }
        catch (SqlException e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
        catch (Exception e)
        {
            ServerSetup.EventsLogger(e.ToString());
        }
    }
}