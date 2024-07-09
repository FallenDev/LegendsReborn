namespace Darkages.Types.Buffs;

public class Mines : BuffBase
{
    public override byte Icon => 177;
    public override int Length => 1800;
    public override string Name => "mines";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling)
            affected.Client.SendMessage(0x02, "You may enter the mines.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
        {
            aisling.Client.SystemMessage("Your time in the mines has ended.");
            aisling.MinesFloor = 0;
        }

        base.OnEnded(affected);
    }
}