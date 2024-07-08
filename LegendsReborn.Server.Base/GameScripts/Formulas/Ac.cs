using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Sprites;

namespace Darkages.GameScripts.Formulas;

[Script("AC Formula")]
public class Ac(Sprite obj) : FormulaScript
{
    public override long Calculate(Sprite obj, long value)
    {
        var armor = obj._ac;
        var dmgMitigation = armor / 100f;
        var dmgReducedByMitigation = dmgMitigation * value;
        value -= (int)dmgReducedByMitigation;

        if (value <= 0)
            value = 1;

        return value;
    }
}