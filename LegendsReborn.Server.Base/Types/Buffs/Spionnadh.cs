namespace Darkages.Types.Buffs;

public class Spionnadh : BuffBase
{
    public override byte Icon => 116;
    public override int Length => 1800;
    public override string Name => "spionnadh";

    public override bool TryApply(Sprite source, Sprite affected)
    {
        affected.RemoveBuff("beag naomh aite");
        affected.RemoveBuff("naomh aite");
        affected.RemoveBuff("mor naomh aite");
        affected.RemoveBuff("ard naomh aite");
        affected.RemoveBuff("dia naomh aite");
        affected.RemoveBuff("io dia naomh aite ionad");
        affected.RemoveBuff("beannaich");
        affected.RemoveBuff("mor beannaich");
        affected.RemoveBuff("ard beannaich");
        affected.RemoveBuff("fas deireas");
        affected.RemoveBuff("mor fas deireas");
        affected.RemoveBuff("ard fas deireas");
        affected.RemoveBuff("armachd");
        affected.RemoveBuff("mor armachd");
        affected.RemoveBuff("ard armachd");
        affected.RemoveBuff("spionnadh");
        
        OnApplied(affected);

        return true;
    }

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        //always applies itself (removes itself above)
        affected.Buffs[Name] = this;
        
        affected.BonusAc -= 20;
        affected.BonusHit += 8;
        affected.BonusDmg += 8;

        //Also applies NaomhAite.

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Divine blessings wash over you.");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.BonusAc += 20;
        affected.BonusHit -= 8;
        affected.BonusDmg -= 8;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Your divine blessings fade.");

        base.OnEnded(affected);
    }
}