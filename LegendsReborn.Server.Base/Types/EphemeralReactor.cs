using Darkages.Common;

namespace Darkages.Types;

public class EphemeralReactor
{
    private WorldServerTimer _timer;

    public EphemeralReactor(string lpKey, int lpTimeout)
    {
        YamlKey = lpKey;
        _timer = new WorldServerTimer(TimeSpan.FromSeconds(lpTimeout));
    }

    public bool Expired { get; set; }
    public string YamlKey { get; set; }

    public void Update(TimeSpan elapsedTime)
    {
        _timer.Update(elapsedTime);

        if (_timer.Elapsed)
        {
            Expired = true;
            _timer = null;
        }
    }
}