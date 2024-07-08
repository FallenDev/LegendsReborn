using Chaos.Common.Identity;

using Darkages.Common;
using Darkages.ScriptingBase;
using Darkages.Sprites;
using Darkages.Templates;
using Darkages.Types;

namespace Darkages.GameScripts.Creations;

[Script("Create Monster")]
public class CreateMonster(MonsterTemplate template, Area map) : MonsterCreateScript
{
    public override Monster Create()
    {
        var obj = new Monster
        {
            Template = template,
            BashTimer = new WorldServerTimer(TimeSpan.FromMilliseconds(template.AttackSpeed)),
            AbilityTimer = new WorldServerTimer(TimeSpan.FromMilliseconds(template.CastSpeed)),
            CastTimer = new WorldServerTimer(TimeSpan.FromMilliseconds(template.CastSpeed)),
            WalkTimer = new WorldServerTimer(TimeSpan.FromMilliseconds(template.MovementSpeed)),
            ObjectUpdateTimer = new WorldServerTimer(TimeSpan.FromMilliseconds(ServerSetup.Instance.Config.GlobalBaseSkillDelay)),
            CastEnabled = true,
            Serial = EphemeralRandomIdGenerator<uint>.Shared.NextId,
            Size = "",
            CurrentMapId = map.ID
        };

        return obj;
    }
}