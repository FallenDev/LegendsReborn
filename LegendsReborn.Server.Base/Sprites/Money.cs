using Chaos.Common.Definitions;
using Chaos.Common.Identity;

using Darkages.Common;
using Darkages.Types;
using Darkages.Object;

namespace Darkages.Sprites;

public sealed class Money : Sprite
{
    public uint Amount { get; set; }

    public ushort Image { get; set; }
    public MoneySprites Type { get; set; }

    public static void Create(Sprite parent, uint amount, Position location)
    {
        if (parent == null)
            return;

        var money = new Money();
        money.CalcAmount(amount);

        lock (Generator.Random)
            money.Serial = EphemeralRandomIdGenerator<uint>.Shared.NextId;

        money.AbandonedDate = DateTime.UtcNow;
        money.CurrentMapId = parent.CurrentMapId;
        money.XPos = location.X;
        money.YPos = location.Y;

        var mt = (int) money.Type;

        if (mt > 0) 
            money.Image = (ushort) (mt + 0x8000);

        ObjectManager.AddObject(money);
    }

    public void GiveTo(uint amount, Aisling aisling)
    {
        if (aisling.GoldPoints + amount < ServerSetup.Instance.Config.MaxCarryGold)
        {
            aisling.GoldPoints += amount;

            if (aisling.GoldPoints > ServerSetup.Instance.Config.MaxCarryGold)
                aisling.GoldPoints = int.MaxValue;

            aisling.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"You've received {amount} coin(s).");
            aisling.Client.SendAttributes(StatUpdateType.ExpGold);

            Remove();
        }
    }

    private void CalcAmount(uint amount)
    {
        Amount = amount;

        if ((Amount > 0) && (Amount < 10))
            Type = MoneySprites.SilverCoin;

        if ((Amount >= 10) && (Amount < 100))
            Type = MoneySprites.GoldCoin;

        if ((Amount >= 100) && (Amount < 1000))
            Type = MoneySprites.SilverPile;

        if (Amount >= 1000)
            Type = MoneySprites.GoldPile;
    }

    public enum MoneySprites : short
    {
        GoldCoin = 0x0089,
        SilverCoin = 0x008A,
        CopperCoin = 0x008B,
        GoldPile = 0x008C,
        SilverPile = 0x008D,
        CopperPile = 0x008E
    }
}