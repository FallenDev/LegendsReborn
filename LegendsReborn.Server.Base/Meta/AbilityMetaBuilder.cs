using Darkages.Enums;
using Darkages.Models;
using Darkages.Network.Server;

namespace Darkages.Meta;

public abstract class AbilityMetaBuilder : MetafileManager
{
    public static void AbilityMeta()
    {
        foreach (var abilityTuple in WorldServer.SkillMap)
        {
            var sClass = new Metafile { Name = abilityTuple.Value, Nodes = [] };
            var class1 = abilityTuple.Key.path;
            var class2 = abilityTuple.Key.pastClass;

            SkillBuilder(sClass, class1, class2);
            sClass.Nodes.Add(new MetafileNode("", ""));
            SpellBuilder(sClass, class1, class2);
            CompileTemplate(sClass);
            Metafiles.Add(sClass);
        }
    }

    private static void SkillBuilder(Metafile sClass, Class currentClass, Class previousClass)
    {
        sClass.Nodes.Add(new MetafileNode("Skill", ""));

        foreach (var template in ServerSetup.Instance.GlobalSkillTemplateCache
                     .Where(p => p.Value.Prerequisites != null)
                     .OrderBy(p => p.Value.Prerequisites.ExpLevelRequired)
                     .Select(p => p.Value)
                     .Distinct())
        {
            if (template.Prerequisites.ClassRequired == currentClass ||
                  template.Prerequisites.ClassRequired == Class.Peasant ||
                 template.Prerequisites.SecondaryClassRequired == previousClass)
            {
                sClass.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }
        }

        sClass.Nodes.Add(new MetafileNode("Skill_End", ""));
    }

    private static void SpellBuilder(Metafile sClass, Class currentClass, Class previousClass)
    {
        sClass.Nodes.Add(new MetafileNode("Spell", ""));

        foreach (var template in ServerSetup.Instance.GlobalSpellTemplateCache
                     .Where(p => p.Value.Prerequisites != null)
                     .OrderBy(p => p.Value.Prerequisites.ExpLevelRequired)
                     .Select(p => p.Value)
                     .Distinct())
        {
            if (template.Prerequisites.ClassRequired == currentClass ||
                  template.Prerequisites.ClassRequired == Class.Peasant ||
                 template.Prerequisites.SecondaryClassRequired == previousClass)
            {
                sClass.Nodes.Add(new MetafileNode(template.Prerequisites.DisplayName, template.GetMetaData()));
            }
        }

        sClass.Nodes.Add(new MetafileNode("Spell_End", ""));
    }
}