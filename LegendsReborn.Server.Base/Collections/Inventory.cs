using System.Diagnostics.CodeAnalysis;
using Chaos.Extensions.Common;
using Darkages.Collections.Abstractions;
using Darkages.Collections.Helpers;
using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Sprites;

namespace Darkages.Collections;

public class Inventory : DisplayedStoredPanel<Item>
{
    private readonly Aisling Aisling;

    public int AvailableSlots
    {
        get
        {
            lock (Sync)
                return TotalSlots - Objects.Count(obj => obj != null);
        }
    }

    public Item this[string name]
    {
        get
        {
            lock (Sync)
                return Objects.FirstOrDefault(obj => obj?.Template?.Name?.EqualsI(name) == true);
        }
    }

    public Inventory(Aisling aisling)
        : base(Pane.Inventory, 60, [0], new PlayersInventoryStorage(aisling), new ItemDisplay(aisling.Client))
    {
        Aisling = aisling;
        
        var items = Storage?.Load() ?? Enumerable.Empty<Item>();
        
        foreach (var item in items.Where(s => s.InventorySlot is not 0))
        {
            if (!ServerContext.GlobalItemTemplateCache.TryGetValue(item.Name, out var template))
                continue;
            
            item.Template = template;
            item.AbandonedDate = DateTime.UtcNow;
            item.OffenseElement = item.Template.OffenseElement;
            item.DefenseElement = item.Template.DefenseElement;
            item.Warnings = [false, false, false, false];
            item.AuthenticatedAislings = null;

            if (item.Color == 0)
                item.Color = (byte)ServerContext.Config.DefaultItemColor;
            
            if (item.Template.Flags.HasFlag(ItemFlags.Repairable))
            {
                if (item.Template.MaxDurability == uint.MinValue)
                {
                    item.Template.MaxDurability = ServerContext.Config.DefaultItemDurability;
                    item.Durability = ServerContext.Config.DefaultItemDurability;
                }

                if (item.Template.Value == uint.MinValue)
                    item.Template.Value = ServerContext.Config.DefaultItemValue;
            }

            if (item.Template.Flags.HasFlag(ItemFlags.QuestRelated))
            {
                item.Template.MaxDurability = 0;
                item.Durability = 0;
            }
            
            item.Scripts = ScriptManager.Load<ItemScript>(item.Template.ScriptName, item);

            if (!string.IsNullOrEmpty(item.Template.WeaponScript))
                item.WeaponScripts = ScriptManager.Load<WeaponScript>(item.Template.WeaponScript, item);
            
            while (Objects[item.InventorySlot] != null)
                item.InventorySlot++;
            
            Objects[item.InventorySlot] = item;
        }
    }

    public override bool TryAdd(Item obj, byte slot)
    {
        if (!IsValidSlot(slot))
            return false;

        lock (Sync)
        {
            if (TryAddStack(obj))
                return true;

            Aisling.CurrentWeight += obj.Template.CarryWeight;
            Aisling.Client.SendStats(StatusFlags.Primary);

            if (Objects[slot] == null)
            {
                obj.InventorySlot = slot;
                obj.Serial = Aisling.Serial;
                obj.Owner = Aisling.Serial;
                
                Objects[slot] = obj;
                Storage.Add(obj);
                Display.Display(obj);

                return true;
            }
            return false;
        }
    }

    public override bool Remove(byte slot)
    {
        lock (Sync)
            return TryGetRemove(slot, out _);
    }

    public bool Remove(string name)
    {
        lock (Sync)
        {
            var first = Objects.FirstOrDefault(obj => obj?.Template?.Name?.EqualsI(name) == true);

            if (first != null)
                return Remove(first.InventorySlot);

            return false;
        } 
    }

    public void RemoveAll(string name)
    {
        lock (Sync)
        {
            var itemsToRemove = Objects.Where(obj => obj?.Template?.Name?.EqualsI(name) == true).ToList();

            foreach (var item in itemsToRemove)
                Remove(item.InventorySlot);
        }
    }
    
    public override bool TryGetRemove(byte slot, out Item obj)
    {
        lock (Sync)
        {
            if (base.TryGetRemove(slot, out obj))
            {
                obj.InventorySlot = 0;
                obj.Serial = obj.ItemId;
                Aisling.CurrentWeight -= obj.Template.CarryWeight;
                Aisling.Client.SendStats(StatusFlags.Primary);

                return true;
            }

            return false;
        }
    }

    public Item FindInSlot(int slot)
    {
            var item = Snapshot(i => i.InventorySlot == slot).FirstOrDefault();
        if (item != null)
            return item;

        return null;       
        
    }
    public override bool TryAddToNextSlot(Item obj)
    {
        lock (Sync)
            return TryAddStack(obj) || base.TryAddToNextSlot(obj);
    }

    private bool TryAddStack(Item obj)
    {
        if (!obj.Template.CanStack)
            return false;

        if (obj.Stacks == 0)
            obj.Stacks = 1;
        
        foreach(var item in Objects.Where((item, index) => (item != null) && IsValidSlot((byte)index)))
            if (item.Template.Name.Equals(obj.Template.Name, StringComparison.OrdinalIgnoreCase))
                if (item.Stacks < item.Template.MaxStack)
                {
                    var incomingStacks = Math.Max(1, (int)obj.Stacks);
                    var existingStacks = Math.Max(1, (int)item.Stacks);
                    var amountPossible = Math.Clamp(item.Template.MaxStack - existingStacks, 1, item.Template.MaxStack);
                    var amountToAdd = Math.Clamp(obj.Stacks, 1, amountPossible);

                    item.Stacks = (ushort)(existingStacks + amountToAdd);
                    obj.Stacks = (ushort)Math.Clamp(incomingStacks - amountToAdd, 0, obj.Stacks);
                    Storage.Update(item);
                    Display.Display(item);

                    //TODO: anything else to update?

                    if (obj.Stacks <= 0)
                        return true;
                }

        return false;
    }

    public bool RemoveQuantity(string name, int amount)
    {
        lock (Sync)
        {
            var matchingItems = Objects.Where(item => (item != null) && item.Template.Name.EqualsI(name)).ToList();
            var total = matchingItems.Sum(item => Math.Max(1U, item.Stacks));

            if (total < amount)
                return false;

            foreach (var item in matchingItems)
            {
                var stacks = (int)Math.Max(1U, item.Stacks);
                var slot = item.InventorySlot;

                if (stacks <= amount)
                {
                    Remove(slot);
                    amount -= stacks;
                } else
                {
                    item.Stacks -= (ushort)amount;
                    Storage.Update(item);
                    Display.Display(item);
                    amount = 0;
                }

                if (amount <= 0)
                    break;
            }

            return true;
        }
    }

    public bool RemoveQuantity(string name, int amount, [MaybeNullWhen(false)] out List<Item> items)
    {
        items = null;

        lock (Sync)
        {
            var matchingItems = Objects.Where(item => (item != null) && item.Template.Name.EqualsI(name)).ToList();
            var total = matchingItems.Sum(item => Math.Max(1U, item.Stacks));

            if (total < amount)
                return false;

            items = [];

            foreach (var item in matchingItems)
            {
                var stacks = (int)Math.Max(1U, item.Stacks);
                var slot = item.InventorySlot;

                if (stacks <= amount)
                {
                    Remove(slot);
                    amount -= stacks;
                    items.Add(item);
                }
                else
                {
                    item.Stacks -= (ushort)amount;
                    Storage.Update(item);
                    Display.Display(item);
                    var newItem = ObjectManager.Clone(item);
                    newItem.Stacks = (ushort)amount;
                    items.Add(newItem);
                    amount = 0;
                }

                if (amount <= 0)
                    break;
            }

            return true;
        }
    }

    public override bool TrySwap(byte slot1, byte slot2)
    {
        //if either slot is invalid, false
        if (!IsValidSlot(slot1) || !IsValidSlot(slot2))
            return false;

        lock (Sync)
        {
            var obj1 = Objects[slot1];
            var obj2 = Objects[slot2];

            if (obj1 != null)
                obj1.InventorySlot = slot2;
            
            if (obj2 != null)
                obj2.InventorySlot = slot1;

            Objects[slot1] = obj2;
            Objects[slot2] = obj1;

            if (obj1 != null)
            {
                Storage.Update(obj1);
                Display.Display(obj1);
            } else
                Display.Remove(slot2);

            if (obj2 != null)
            {
                Storage.Update(obj2);
                Display.Display(obj2);
            } else
                Display.Remove(slot1);

            return true;
        }
    }

    public IEnumerable<Item> GetAll(string name) => Snapshot(item => item.Template.Name.EqualsI(name));

    public int CountOf(string name)
    {
        lock (Sync)
            return Objects
                .Where(obj => (obj != null) && obj.Template.Name.EqualsI(name))
                .Select(obj => Math.Max(1, (int)obj.Stacks))
                .Sum();
    }

    public bool HasCount(string name, int amount) => CountOf(name) >= amount;

    public bool ContainsItem(string name)
    {
        lock (Sync)
            return Objects.Any(obj => obj?.Template?.Name.EqualsI(name) == true);
    }
    
    #region idk about these, maybe look at removing them in the future
    public IEnumerable<Item> GetGodItems(string godName = null)
    {
        var snapshot = Snapshot(item => item.Template.Flags.HasFlag(ItemFlags.Equipable));

        if (!string.IsNullOrEmpty(godName))
            return snapshot.Where(item => item.Template.Name.ContainsI(godName));

        return snapshot.Where(item =>
            item.Template.Name.ContainsI("Deoch")
            || item.Template.Name.ContainsI("Sgrios")
            || item.Template.Name.ContainsI("Gramail")
            || item.Template.Name.ContainsI("Luathas")
            || item.Template.Name.ContainsI("Ceannlaidir")
            || item.Template.Name.ContainsI("Fiosachd")
            || item.Template.Name.ContainsI("Glioca")
            || item.Template.Name.ContainsI("Cail"));
    }
    public IEnumerable<Item> GetCrystals(string crystalName = null)
    {
        var snapshot = Snapshot(item => item.Template.Flags.HasFlag(ItemFlags.SpecialNoStack));
        if (!string.IsNullOrEmpty(crystalName))
            return snapshot.Where(item => item.Template.Name.ContainsI(crystalName));
        return snapshot.Where(item =>
        item.Template.Name.ContainsI("Crystal Shard"));
    }
    public IEnumerable<Item> GetEquippableItems(string equipType = null)
    {
        var snapshot = Snapshot(item => item.Template.Flags.HasFlag(ItemFlags.Equipable));

        if (!string.IsNullOrEmpty(equipType))
        {
            if (equipType.EqualsI("Ring"))
                return snapshot.Where(item => item.Template.Name.ContainsI(equipType) && !item.Template.Name.ContainsI("Earring"));

            return snapshot.Where(item => item.Template.Name.ContainsI(equipType));
        }

        return snapshot.Where(item =>
            item.Template.Name.ContainsI("Boots")
            || item.Template.Name.ContainsI("Bracer")
            || item.Template.Name.ContainsI("Ring") //earrings are implicit
            || item.Template.Name.ContainsI("Gauntlet")
            || item.Template.Name.ContainsI("Greaves")
            || item.Template.Name.ContainsI("Shield"));
    }
    #endregion

}