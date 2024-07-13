using Darkages.Interfaces;
using Darkages.Sprites;

namespace Darkages.ScriptingBase;

public abstract class RewardScript : IScriptBase
{
    public abstract void GenerateRewards(Monster monster, Aisling player);
}