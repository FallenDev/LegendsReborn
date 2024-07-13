using Darkages.Interfaces;
using Darkages.Sprites;

namespace Darkages.ScriptingBase;

public abstract class FormulaScript : IScriptBase
{
    public abstract int Calculate(Sprite obj, int value);
}