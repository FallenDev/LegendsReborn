namespace Darkages.Types.Buffs;

public class AsgallFaileas : BuffBase
{
    public override byte Icon => 118;
    public override int Length => 12;
    public override string Name => "asgall faileas";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (!affected.Buffs.TryAdd(Name, this))
            return;

        affected.SkillReflect = true;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Attacks bounce off of you!");

        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Buffs.TryRemove(Name, out _))
            return;

        affected.SkillReflect = false;

        if (affected is Aisling aisling)
            aisling.Client.SendMessage(0x02, "Skills are no longer being reflected.");

        base.OnEnded(affected);
    }
}