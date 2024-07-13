using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Sprites;

namespace Darkages.ScriptingBase;

public abstract class ElementFormulaScript : IScriptBase
{
    public abstract double Calculate(Sprite obj, ElementManager.Element element);
}