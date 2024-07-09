namespace Darkages.Types.Buffs;

public class AiseagSpiorad : BuffBase
{
    public override byte Icon => 211;
    public override int Length => 180;
    public override string Name => "aiseag spiorad";
    
    
    private void ApplyRefresh(Sprite affected)
    {
        const double MODIFIER = 0.01;

        //get the added amount of mp (MaximumMP * 0.01), max of 1k
        var added = Convert.ToInt32(Math.Min(1000, affected.MaximumMp * MODIFIER));
        //add that mana to the affected's current mp (don't exceed max mp)
        affected.CurrentMp = Math.Min(affected.MaximumMp, affected.CurrentMp + added);
    }

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.SendAnimation(168, affected, affected);
        
        if (affected is Aisling aisling)
        {
            aisling.Client.SendMessage(0x02, "You feel inspired.");
            InsertBuff(aisling);
            aisling.Client.SendStats(StatusFlags.All);
        }

        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        affected.SendAnimation(168, affected, affected);
        
        ApplyRefresh(affected);
        
        base.OnDurationUpdate(affected);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "You no longer feel inspired.");

        base.OnEnded(affected);
    }
}