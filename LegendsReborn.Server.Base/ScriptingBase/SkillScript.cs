using Darkages.Interfaces;
using Darkages.Object;
using Darkages.Sprites;
using Darkages.Types;

namespace Darkages.ScriptingBase;

public abstract class SkillScript : ObjectManager, IScriptBase, IUseableTarget
{
    protected SkillScript(Skill skill) => Skill = skill;

    public bool IsScriptDefault { get; set; }

    public Skill Skill { get; set; }

    public abstract void OnFailed(Sprite sprite);

    public abstract void OnSuccess(Sprite sprite, Sprite target);

    public abstract void OnUse(Sprite sprite, Sprite target);
}