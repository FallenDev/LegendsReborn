namespace Darkages.Types.Buffs;

public class Gathering : BuffBase
{
    public override byte Icon => 100;
    public override int Length => 180;
    public override string Name => "gathering";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Aisling)
            return;

        if (!affected.Buffs.TryAdd(Name, this))
            return;

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling)
        {
            var mapId = affected.CurrentMapId;

            if (mapId is >= 99000 and <= 99005)
            {
                affected.Client.TransitionToMap(mapId, new Position(16, 5));
                affected.Client.SendMessage(0x02, "The farmer signals that you've gathered your money's worth.");
            }
        }

        base.OnEnded(affected);
    }
}