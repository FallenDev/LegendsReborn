using Darkages.CommandSystem.Loot;
using Darkages.Common;
using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Templates;
using Darkages.Types;
using Newtonsoft.Json;

namespace Darkages.Sprites;

public sealed class Monster : Sprite
{
    public Monster()
    {
        BashEnabled = false;
        CastEnabled = false;
        WalkEnabled = false;
        TaggedAislings = new HashSet<int>();

        EntityType = TileContent.Monster;
    }
    public override string ToString() => Template.Name;
    public bool Aggressive { get; set; }
    public bool BashEnabled { get; set; }
    public bool CastEnabled { get; set; }
    public ushort Image { get; set; }
    public bool WalkEnabled { get; set; }
    //sets the monster's target to null
    public string Owner { get; set; }
    public WorldServerTimer BashTimer { get; set; }
    public WorldServerTimer CastTimer { get; set; }
    public MonsterTemplate Template { get; set; }
    public WorldServerTimer WanderTimer { get; set; }
    public WorldServerTimer EngagedWalkTimer { get; set; }
    [JsonIgnore] public bool IsAlive => CurrentHp > 0;
    [JsonIgnore] public LootTable LootTable { get; set; }
    [JsonIgnore] public bool Rewarded { get; set; }
    [JsonIgnore] public Dictionary<string, MonsterScript> Scripts { get; set; }
    [JsonIgnore] public bool Skulled { get; set; }
    [JsonIgnore] public HashSet<int> TaggedAislings { get; set; }
    public static Monster Create(MonsterTemplate template, Area map)
    {
        var monsterCreateScript = ScriptManager.Load<MonsterCreateScript>(ServerSetup.Instance.Config.MonsterCreationScript,
                template,
                map)
            .FirstOrDefault();

        var monster = monsterCreateScript.Value?.Create(template, map);
        InitScripting(monster.Template, map, monster);
        return monster;
    }
    public static void InitScripting(MonsterTemplate template, Area map, Monster obj)
    {
        if ((obj.Scripts == null) || !obj.Scripts.Any())
            obj.Scripts = ScriptManager.Load<MonsterScript>(template.ScriptName, obj, map);
    }
    public void GenerateRewards(Aisling player)
    {
        if (Rewarded)
            return;

        if (player?.Client?.Aisling == null)
            return;

        var script = ScriptManager.Load<RewardScript>(ServerSetup.Instance.Config.MonsterRewardScript, this, player).FirstOrDefault();
        script.Value?.GenerateRewards(this, player);

        Rewarded = true;
        player.UpdateStats();
    }
}