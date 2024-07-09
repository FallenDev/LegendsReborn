namespace Darkages.Types.Buffs;

public class Tionac : BuffBase
{
    public override byte Icon => 178;
    public override int Length => 30;
    public override string Name => "tionac";
    
    private void AblativeHeal(Sprite affected)
    {
        //add that health to the affected's current Hp (don't exceed max Hp)
        affected.CurrentHp = Math.Min(affected.MaximumHp, affected.CurrentHp + affected.Ablasion);
    }
    
    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        if (affected is Aisling aisling)
            aisling.Client?.SendMessage(0x02, "A holy ward is protecting you.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnDurationUpdate(Sprite affected)
    {
        if (affected.CurrentHp <= (affected.MaximumHp * 0.5))
        {
            if (affected.CurrentHp <= 0)
            {
                if (affected.RemoveDebuff("skulled", true))
                {
                    if (affected is Monster targetMonster)
                        targetMonster.Skulled = false;
                    
                    affected.Client?.Revive();

                }
            }
            OnEnded(affected);
        }
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        AblativeHeal(affected);
        affected.SendAnimation(78, affected, affected);
        affected.Client?.SendStats(StatusFlags.Vitality);

        if (affected is Aisling aisling)
            aisling.Client?.SendMessage(0x02, "The holy ward has disappeared.");

        base.OnEnded(affected);
    }
}