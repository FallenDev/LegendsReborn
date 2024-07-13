using Chaos.Common.Definitions;
using Darkages.Common;
using Darkages.Enums;
using Darkages.Object;
using Darkages.ScriptingBase;
using Darkages.Templates;
using Darkages.Types;
using Newtonsoft.Json;
using static System.Formats.Asn1.AsnWriter;

namespace Darkages.Sprites;

public class Mundane : Sprite
{
    [JsonIgnore] public List<SkillScript> SkillScripts = new();
    [JsonIgnore] public List<SpellScript> SpellScripts = new();
    [JsonIgnore] public int WaypointIndex;
    [JsonIgnore] private int ChatIdx = new ThreadLocal<int>(() => 0, true).Value;
    [JsonIgnore] public Position CurrentWaypoint => Template.Waypoints[WaypointIndex] ?? null;
    [JsonIgnore] public SkillScript DefaultSkill => SkillScripts.Find(i => i.IsScriptDefault) ?? null;
    [JsonIgnore] public SpellScript DefaultSpell => SpellScripts.Find(i => i.IsScriptDefault) ?? null;
    [JsonIgnore] public Dictionary<string, MundaneScript> Scripts { get; set; }
    public MundaneTemplate Template { get; set; }

    public static void Create(MundaneTemplate template)
    {
        if (template == null)
            return;

        var map = ServerSetup.Instance.GlobalMapCache[template.AreaID];
        var existing = ObjectManager.GetObject<Mundane>(map,
            p => (p != null) && (p.Template != null) && (p.Template.Name == template.Name));

        if (existing != null)
            if (existing.CurrentHp == 0)
                existing.OnDeath();
            else
                return;

        var npc = new Mundane
        {
            Template = template
        };

        if (npc.Template.TurnRate == 0)
            npc.Template.TurnRate = 5;

        if (npc.Template.CastRate == 0)
            npc.Template.CastRate = 2;

        if (npc.Template.WalkRate == 0)
            npc.Template.WalkRate = 3;

        npc.CurrentMapId = npc.Template.AreaID;
        lock (Generator.Random)
            npc.Serial = (uint)Generator.GenerateNumber();

        npc.XPos = template.X;
        npc.YPos = template.Y;
        npc._MaximumHp = 10000000;
        npc._MaximumMp = 0;
        npc.Template.MaximumHp = npc.MaximumHp;
        npc.Template.MaximumMp = npc.MaximumMp;
        npc.CurrentHp = npc.Template.MaximumHp;
        npc.CurrentMp = npc.Template.MaximumMp;
        npc.Direction = npc.Template.Direction;
        npc.CurrentMapId = npc.Template.AreaID;

        npc.BonusAc = -200;

        if (npc.BonusAc < -85) 
            npc.BonusAc = -85;
        if (npc.Template.ChatRate == 0)
            npc.Template.ChatRate = 120;

        if (npc.Template.TurnRate == 0) 
            npc.Template.TurnRate = 8;

        npc.DefenseElement = Generator.RandomEnumValue<ElementManager.Element>();
        npc.OffenseElement = Generator.RandomEnumValue<ElementManager.Element>();

        npc.Scripts = ScriptManager.Load<MundaneScript>(template.ScriptKey, ServerSetup.Instance.Game, npc);

        npc.Template.AttackTimer = new WorldServerTimer(TimeSpan.FromSeconds(3));
        npc.Template.EnableTurning = false;
        npc.Template.EnableWalking = false;
        npc.Template.EnableAttacking = false;
        npc.Template.WalkTimer = new WorldServerTimer(TimeSpan.FromSeconds(npc.Template.WalkRate));
        npc.Template.ChatTimer = new WorldServerTimer(TimeSpan.FromSeconds(npc.Template.ChatRate));
        npc.Template.TurnTimer = new WorldServerTimer(TimeSpan.FromSeconds(npc.Template.TurnRate));
        npc.Template.SpellTimer = new WorldServerTimer(TimeSpan.FromSeconds(npc.Template.CastRate));
        npc.InitMundane();

        ObjectManager.AddObject(npc);
    }

    public void InitMundane()
    {
        if (Template.Spells != null)
            foreach (var spellscriptstr in Template.Spells)
                LoadSpellScript(spellscriptstr);

        if (Template.Skills != null)
            foreach (var skillscriptstr in Template.Skills)
                LoadSkillScript(skillscriptstr);

        LoadSkillScript("Assail", true);
    }

    public void LoadSkillScript(string skillscriptstr, bool primary = false)
    {
        Skill obj;
        var scripts = ScriptManager.Load<SkillScript>(skillscriptstr,
            obj = Skill.Create(1, ServerSetup.Instance.GlobalSkillTemplateCache[skillscriptstr]));

        foreach (var script in scripts.Values)
        {
            obj.NextAvailableUse = DateTime.UtcNow;

            if (script != null)
            {
                script.Skill = obj;
                script.IsScriptDefault = primary;
                SkillScripts.Add(script);
            }
        }
    }

    public void OnDeath()
    {
        RemoveActiveTargets();

        if (CurrentHp == 0)
            CurrentHp = Template.MaximumHp;
    }

    public void Patrol()
    {
        if (CurrentWaypoint != null) 
            WalkTo(CurrentWaypoint.X, CurrentWaypoint.Y);

        if ((Position.DistanceFrom(CurrentWaypoint) <= 2) || (CurrentWaypoint == null))
        {
            if (WaypointIndex + 1 < Template.Waypoints.Count)
                WaypointIndex++;
            else
                WaypointIndex = 0;
        }
    }

    public void Update(TimeSpan update)
    {
        if (Template == null)
            return;

        if (IsConfused || IsFrozen || IsParalyzed || IsSleeping || IsPetrified)
            return;

        if ((Target != null) && !WithinViewOf(Target))
            Target = null;

        var nearby = GetObjects<Aisling>(Map, i => i.WithinViewOf(this));

        if (!nearby.Any()) 
            return;

        if (Template.ChatTimer != null)
        {
            Template.ChatTimer.UpdateTime(update);

            if (Template.ChatTimer.Elapsed && nearby.Any())
            {
                foreach (var obj in nearby)
                    if (Template.Speech.Count > 0)
                    {
                        var idx = Generator.Random.Next(Template.Speech.Count);
                        var msg = Template.Speech[idx];

                        foreach (var aisling in nearby)
                        {
                            aisling.Client.SendPublicMessage(Serial, PublicMessageType.Normal, $"{Template.Name}: {msg}");
                        }
                    }

                Template.ChatTimer.Reset();
            }
        }

        if (Template.EnableTurning)
            if (Template.TurnTimer != null)
            {
                Template.TurnTimer.UpdateTime(update);
                if (Template.TurnTimer.Elapsed)
                {
                    lock (Generator.Random)
                        Direction = (byte) Generator.Random.Next(0, 4);

                    Turn();

                    Template.TurnTimer.Reset();
                }
            }

        if (Template.EnableCasting)
        {
            Template.SpellTimer.UpdateTime(update);

            if (Template.SpellTimer.Elapsed)
            {
                if ((Target == null) || (Target.CurrentHp == 0) || !Target.WithinViewOf(this))
                {
                    var targets = GetObjects<Monster>(Map, i => i.WithinViewOf(this))
                        .OrderBy(i => i.Position.DistanceFrom(Position));

                    foreach (var t in targets) t.Target = this;

                    var target = Target ?? targets.FirstOrDefault();

                    if (target?.CurrentHp == 0)
                        target = null;

                    if (!CanCast)
                        return;

                    Target = target;
                }

                if ((Target != null) && (Target != null) && (SpellScripts.Count > 0))
                {
                    var idx = 0;
                    lock (Generator.Random)
                        idx = Generator.Random.Next(SpellScripts.Count);

                    SpellScripts[idx].OnUse(this, Target);
                }

                Template.SpellTimer.Reset();
            }
        }

        if (Template.EnableAttacking)
            if ((Template.AttackTimer != null))
            {
                Template.AttackTimer.UpdateTime(update);
                if (Template.AttackTimer.Elapsed)
                {
                    var targets = GetObjects<Monster>(Map, i => i.WithinViewOf(this))
                        .OrderBy(i => i.Position.DistanceFrom(Position));

                    foreach (var t in targets)
                        t.Target = this;

                    var target = Target ?? targets.FirstOrDefault();

                    if (target?.CurrentHp == 0)
                        target = null;

                    if (target != null)
                    {
                        foreach (var script in Scripts.Values)
                            script?.TargetAcquired(target);

                        if (!Position.IsNextTo(target.Position))
                            WalkTo(target.XPos, target.YPos);
                        else
                        {
                            if (!Facing(target, out var direction))
                            {
                                Direction = (byte)direction;
                                Turn();
                            }
                            else
                            {
                                target.Target = this;
                                DefaultSkill?.OnUse(this, target);

                                if ((SkillScripts.Count > 0) && (target.Target != null))
                                {
                                    var obj = SkillScripts.FirstOrDefault(i => i.Skill.CanUse());

                                    if (obj != null)
                                    {
                                        var skill = obj.Skill;

                                        obj?.OnUse(this, target);
                                        {
                                            skill.InUse = true;

                                            if (skill.Template.Cooldown > 0)
                                                skill.NextAvailableUse =
                                                    DateTime.UtcNow.AddSeconds(skill.Template.Cooldown);
                                            else
                                                skill.NextAvailableUse =
                                                    DateTime.UtcNow.AddMilliseconds(ServerSetup.Instance.Config
                                                        .GlobalBaseSkillDelay);
                                        }

                                        skill.InUse = false;
                                    }
                                }
                            }
                        }
                    }
                    Template.AttackTimer.Reset();
                }
            }
        if (Template.EnableWalking)
            if (Template.WalkTimer != null)
            {
                Template.WalkTimer.UpdateTime(update);
                if (Template.WalkTimer.Elapsed)
                {
                    Wander();
                    Template.WalkTimer.Reset();
                }
            }
    }

    private void LoadSpellScript(string spellscriptstr, bool primary = false)
    {
        var scripts = ScriptManager.Load<SpellScript>(spellscriptstr,
            Spell.Create(1, ServerSetup.Instance.GlobalSpellTemplateCache[spellscriptstr]));

        foreach (var script in scripts.Values)
            if (script != null)
            {
                script.IsScriptDefault = primary;
                SpellScripts.Add(script);
            }
    }

    private void RemoveActiveTargets()
    {
        var nearbyMonsters = GetObjects<Monster>(Map, i => WithinViewOf(this));
        foreach (var nearby in nearbyMonsters)
            if ((nearby.Target != null) && (nearby.Target.Serial == Serial))
                nearby.Target = null;
    }
}