namespace Darkages.Types.Debuffs;

public class Investigation : DebuffBase
{
    public override byte Icon => 203;
    public override int Length => 180;
    public override string Name => "investigation";

    public override void OnApplied(Sprite affected, int? timeLeft = null)
    {
        if (affected is Aisling aisling)
        {
            aisling.Client.SystemMessage("You are under investigation for botting.");
        }

        if (!affected.Debuffs.TryAdd(Name, this))
            return;
        
        base.OnApplied(affected, timeLeft);
    }

    public override void OnEnded(Sprite affected)
    {
        if (!affected.Debuffs.TryRemove(Name, out _))
            return;

        if (!Cancelled)
        {
            if (affected is Aisling aisling)
                aisling.Client.SendMessage(0x02, "You have been arrested for botting.");
            var random = new Random();
            var cell = random.Next(1, 5);

            switch (cell)
            {
                case 1:
                    affected.Client.TransitionToMap(17005, new Position(3, 4));

                    break;
                case 2:
                    affected.Client.TransitionToMap(17005, new Position(10, 5));

                    break;
                case 3:
                    affected.Client.TransitionToMap(17005, new Position(17, 4));

                    break;
                case 4:
                    affected.Client.TransitionToMap(17005, new Position(25, 4));

                    break;
            }
        }
        else
        {
            if (affected is Aisling aisling)
                aisling.Client.SystemMessage("Thank you for your cooperation.");
        }

        base.OnEnded(affected);
    }
}