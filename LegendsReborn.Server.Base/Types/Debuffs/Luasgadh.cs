namespace Darkages.Types.Debuffs;

public class Luasgadh : DebuffBase
{
    public override byte Icon => 143;
    public override int Length => 30;
    public override string Name => "luasgadh";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is not Monster monster)
            return;
        
        if (!affected.Debuffs.TryAdd(Name, this))
            return;

        //x = 1.5y
        monster.BashTimer.Delay *= 1.5;
        monster.WanderTimer.Delay *= 1.5;
        monster.EngagedWalkTimer.Delay *= 1.5;
        monster.BashTimer.BaseDelay *= 1.5;
        monster.WanderTimer.BaseDelay *= 1.5;
        monster.EngagedWalkTimer.BaseDelay *= 1.5;
        monster.CastTimer.Delay *= 1.25;

        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (affected is not Monster monster)
            return;

        monster.BashTimer.Delay /= 1.5;
        monster.WanderTimer.Delay /= 1.5;
        monster.EngagedWalkTimer.Delay /= 1.5;
        monster.BashTimer.BaseDelay /= 1.5;
        monster.WanderTimer.BaseDelay /= 1.5;
        monster.EngagedWalkTimer.BaseDelay /= 1.5;
        monster.CastTimer.Delay /= 1.25;

        base.OnEnded(affected);
    }
}