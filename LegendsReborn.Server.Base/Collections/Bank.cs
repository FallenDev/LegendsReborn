using Darkages.Enums;
using Darkages.Sprites;

namespace Darkages.Collections;

public class Bank
{
    //client.SendMessage(0x02, $"{itemName} is too heavy.");
    protected IBetterStorage<Item> Storage { get; }
    protected object Sync { get; } = new();
    protected Dictionary<string, Item> Items { get; }
    protected Aisling Aisling { get; }

    public bool HasItems => Items.Any();

    public Item this[string name]
    {
        get
        {
            lock (Sync)
                return Items.TryGetValue(name, out var item) ? item : null;
        }
    }
    
    public Bank(Aisling aisling)
    {
        Aisling = aisling;
        Storage = new PlayersBankedStorage(aisling);
        Items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        var items = Storage.Load();

        foreach (var item in items)
        {
            if (ServerContext.GlobalItemTemplateCache.TryGetValue(item.Name, out var template))
            {
                item.Template = template;
                item.Image = item.Template.Image;
                item.Color = (byte)item.Template.Color;
                item.DisplayImage = item.Template.DisplayImage;
                item.Name = item.Template.Name;
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
            }
            
            item.Serial = Aisling.Serial;
            item.Owner = Aisling.Serial;

            if (item.Stacks == 0)
                item.Stacks = 1;

            Items[item.Template.Name] = item;
        }
    }

    public bool TryWithdraw(string itemName, int? amount = null)
    {
        lock (Sync)
        {
            if (amount is null or 0)
                amount = 1;

            if (amount < 1)
                return false;

            if (!Items.TryGetValue(itemName, out var item) || (item.Stacks < amount))
                return false;
            
            if (item.Stacks == amount)
            {
                Items.Remove(itemName);
                Storage.Remove(item);
            } else
            {
                item.Stacks -= (ushort)amount;
                Storage.Update(item);
            }

            return true;
        }
    }

    public void Deposit(Item item)
    {
        lock (Sync)
        {
            if (item.Stacks == 0)
                item.Stacks = 1;

            if (!Items.TryGetValue(item.Template.Name, out var existingItem))
            {
                existingItem = item;
                existingItem.Serial = Aisling.Serial;
                existingItem.Owner = Aisling.Serial;
                Items[item.Template.Name] = existingItem;
                Storage.Add(existingItem);
            } else
            {
                existingItem.Stacks += item.Stacks;
                Storage.Update(existingItem);
            }
        }
    }

    public virtual IEnumerable<Item> Snapshot(Func<Item, bool> predicate = null)
    {
        List<Item> snapshot;

        lock (Sync)
            snapshot = Items.Values.ToList();

        using var enumerator = snapshot.GetEnumerator();

        while (enumerator.MoveNext())
            if ((enumerator.Current != null)
                && (predicate?.Invoke(enumerator.Current) ?? true))
                yield return enumerator.Current;
    }

    public bool TryGetItem(string itemName, out Item item)
    {
        lock (Sync)
            return Items.TryGetValue(itemName, out item);
    }

    public bool ContainsItem(string itemName)
    {
        lock (Sync)
            return Items.ContainsKey(itemName);
    }
    
    public int CountOf(string itemName)
    {
        lock (Sync)
            return Items.TryGetValue(itemName, out var item) ? Math.Max(1, (int)item.Stacks) : 0;
    }

    public void Save() => Storage.Save();
}