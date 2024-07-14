namespace Darkages.Types.Debuffs;

public class Elixir : DebuffBase
{
    public override byte Icon => 152;
    public override int Length => 60;
    public override string Name => "elixir";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        affected.Client.UpdateDisplay();
        affected.Client.SendMessage(0x02, "Your clothing is splashed with dye!");
        affected.SendAnimation(205, affected, affected);
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (!Cancelled)
        {
            Sideline(affected.Client);
            affected.Client.SendMessage(0x02, "You have been eliminated!");
        }

        affected.Client.UpdateDisplay();
        base.OnEnded(affected);
    }
    public void Sideline(GameClient affected)
    {
        ushort sidelineX = 0;
        ushort sidelineY = 0;
        if (affected.Aisling.Team == 1)
        {
            sidelineX = 16;
            sidelineY = 4;
            affected.Aisling.EquipmentManager.RemoveFromExisting(1);
            affected.Aisling.Inventory.RemoveAll("Elixir League Bow");
        }
        else if (affected.Aisling.Team == 2)
        {
            sidelineX = 16;
            sidelineY = 40;
            affected.Aisling.EquipmentManager.RemoveFromExisting(1);
            affected.Aisling.Inventory.RemoveAll("Elixir League Bow");
        }
        
        affected.WarpTo(new Position(sidelineX, sidelineY));
    }
}