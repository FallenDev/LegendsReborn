using Darkages.Interfaces;
using Darkages.Object;
using Darkages.Sprites;
using Darkages.Types;

namespace Darkages.ScriptingBase;

public abstract class SpellScript : ObjectManager, IScriptBase, IUseableTarget
{
    public string Arguments { get; set; }
    public bool IsScriptDefault { get; set; }
    public Spell Spell { get; set; }
    protected SpellScript(Spell spell) => Spell = spell;

    public abstract void OnFailed(Sprite source, Sprite target);

    public abstract void OnSuccess(Sprite source, Sprite target);

    public virtual void OnTriggeredBy(Sprite sprite, Sprite target) { }

    public abstract void OnUse(Sprite source, Sprite target);
}