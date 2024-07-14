using Darkages.Common;
using Darkages.Enums;
using Darkages.Sprites;
using Darkages.Templates;
using System.Text;
using Chaos.Common.Definitions;
using Darkages.Network.Client;

namespace Darkages.Types;

public class ItemPredicate
{
    public int AmountRequired { get; set; }
    public bool IsMet { get; set; }
    public string Item { get; set; }

    public void Validate(WorldClient client)
    {
        if ((client?.Aisling != null) && !string.IsNullOrEmpty(Item))
            IsMet = client.Aisling.Inventory
                        .Snapshot(item => item.Template.Name.EqualsI(Item))
                        .Select(item => (int)item.Stacks)
                        .Sum()
                    >= AmountRequired;
    }
}

public class LearningPredicate
{
    public List<ushort> Areas_Visited_Required = [];
    public List<ItemPredicate> Items_Required = [];
    public List<string> Quests_Completed_Required = [];
    private Template _template;

    public LearningPredicate(Template template) => _template = template;

    public LearningPredicate() => _template = null;

    //Pill Fix - Allow mundanes to show multiclass abilities
    #region multiclass
    public string DisplayName { get; set; }
    public Class Multiclass_Required { get; set; }
    public Class Displayclass_Required { get; set; }
    #endregion multiclass
    public Class Class_Required { get; set; }
    public int Con_Required { get; set; }
    public int Dex_Required { get; set; }
    public int ExpLevel_Required { get; set; }
    public int Gold_Required { get; set; }
    public int Int_Required { get; set; }
    public int First_Ability_Level_Required { get; set; }
    public string First_Ability_Required { get; set; }
    public int Second_Ability_Level_Required { get; set; }
    public string Second_Ability_Required { get; set; }
    public ClassStage Stage_Required { get; set; }
    public int Str_Required { get; set; }
    public int Wis_Required { get; set; }

    internal string[] MetaData
        =>
        [
            // ExpLevel Required / Stage Required / AB Required
            $"{(ExpLevel_Required > 0 ? ExpLevel_Required : 0)}/{(Stage_Required != ClassStage.Class ? 1 : 0)}/0",
            $"{(_template is SkillTemplate template ? template.Icon : ((SpellTemplate) _template).Icon)}/0/0",
            //Pill Fix - Placed Con after Dex to fix display error on paper doll page.
            $"{(Str_Required == 0 ? 3 : Str_Required)}/{(Int_Required == 0 ? 3 : Int_Required)}/{(Wis_Required == 0 ? 3 : Wis_Required)}/{(Dex_Required == 0 ? 3 : Dex_Required)}/{(Con_Required == 0 ? 3 : Con_Required)}",
            //Pill Fix - Work on displaying multiple requirements for skills/spells > should be easy to do.
            $"{(!string.IsNullOrEmpty(First_Ability_Required) ? First_Ability_Required : "0")}/{(First_Ability_Level_Required > 0 ? First_Ability_Level_Required : 0)}",
            $"{(!string.IsNullOrEmpty(Second_Ability_Required) ? Second_Ability_Required : "0")}/{(Second_Ability_Level_Required > 0 ? Second_Ability_Level_Required : 0)}",
            $"{(_template.Description != "" ? _template.Description : _template.Name)} \n${{=eCoins Required:{{=u {(Gold_Required > 0 ? Gold_Required : 0)} ${{=eItems Required:{{=u {(Items_Required.Count > 0 ? string.Join(", ", Items_Required.Select(i => i.Item + $"({i.AmountRequired})")) : "None")}\n{Script}" +
            $"\n{{=eAvailable to: {{=u{(Stage_Required == ClassStage.Pure_Master ? $"Pure Master {Class_Required}s" : Stage_Required == ClassStage.Pure_Grand_Master ? $"Pure Grand Master {Class_Required}s" : Stage_Required == ClassStage.Subpathed_Master ? $"All Master {Class_Required}s" : Stage_Required == ClassStage.Subpathed_Grand_Master ? $"All Grand Master {Class_Required}s" : $"All " + (Class_Required == Class.Peasant ? "Classes" : $"{Class_Required}s"))}"
        ];
    //Pill Fix - Allows metadata to show dummy mundane names
    private string Script =>
        _template is SkillTemplate
            ? AreaAndPosition((_template as SkillTemplate)?.MundaneName) ?? "{=eLocation:{=u Unknown"
            : AreaAndPosition((_template as SpellTemplate)?.MundaneName) ?? "{=eLocation:{=u Unknown";

    public void AssociatedWith<T>(T template) where T : Template => _template = template;

    public bool SkillPrerequisiteIsMet(Aisling player, Action<string, bool> callbackMsg = null)
    {
        if ((ServerSetup.Instance.Config.DevModeExemptions != null) && player.GameMaster &&
            ServerSetup.Instance.Config.DevModeExemptions.Contains("learning_predicates"))
            return true;

        var result = new Dictionary<int, Tuple<bool, object>>();
        var n = 0;
        try
        {
            n = CheckSkillPredicates(player, result, n);
            n = CheckAttributePredicates(player, result, n);
            n = CheckItemPredicates(player, result, n);
        }
        catch (Exception e)
        {
            player.Client.CloseDialog();
            player.Client.SendServerMessage(ServerMessageType.OrangeBar2, "Your mind reels at the complex nature of this secret.");
            ServerSetup.EventsLogger($"{e}\nUnhandled exception in {nameof(SkillPrerequisiteIsMet)}.");

            return false;
        }

        var ready = CheckPredicates(callbackMsg, result);
        {
            if (ready)
                player.SendAnimation(22, player, player);
        }

        return ready;
    }
    public bool SpellPrerequisiteIsMet(Aisling player, Action<string, bool> callbackMsg = null)
    {
        //if ((ServerContext.Config.DevModeExemptions != null) && player.GameMaster &&
        //    ServerContext.Config.DevModeExemptions.Contains("learning_predicates"))
        //    return true;

        var result = new Dictionary<int, Tuple<bool, object>>();
        var n = 0;
        try
        {
            n = CheckSpellPredicates(player, result, n);
            n = CheckAttributePredicates(player, result, n);
            n = CheckItemPredicates(player, result, n);
        }
        catch (Exception e)
        {
            player.Client.CloseDialog();
            player.Client.SendServerMessage(ServerMessageType.OrangeBar2, "Your mind reels at the complex nature of this secret.");
            ServerSetup.EventsLogger($"{e}\nUnhandled exception in {nameof(SkillPrerequisiteIsMet)}.");

            return false;
        }

        var ready = CheckPredicates(callbackMsg, result);
        {
            if (ready)
                player.SendAnimation(22, player, player);
        }

        return ready;
    }
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append(
            $"Stats Required: ({Str_Required} STR, {Int_Required} INT, {Wis_Required} WIS, {Con_Required} CON, {Dex_Required} DEX)");
        //Pill Fix - changed from ability to secret.
        sb.Append("\nDo you wish to learn this secret?");
        return sb.ToString();
    }

    private static bool CheckPredicates(Action<string, bool> callbackMsg,
        Dictionary<int, Tuple<bool, object>> result)
    {
        if ((result == null) || (result.Count == 0))
            return false;

        var predicate_result = result.ToList().TrueForAll(i => i.Value.Item1);

        if (predicate_result)
        {
            //Pill Fix - changed from ability to secret.
            callbackMsg?.Invoke("You are ready to learn this secret, Do you wish to proceed?", true);
            return true;
        }

        var sb = string.Empty;
        {
            var errorCaps = result.Select(i => i.Value).Distinct();

            sb += "{=cYou are not worthy of this secret. \n{=u";
            foreach (var predicate in errorCaps)
                if ((predicate != null) && !predicate.Item1)
                    sb += (string)predicate.Item2 + "\n";
        }

        callbackMsg?.Invoke(sb, false);
        return false;
    }

    private string AreaAndPosition(string MundaneName)
    {
        if (string.IsNullOrEmpty(MundaneName))
            MundaneName = "none";

        if (!ServerSetup.Instance.GlobalMundaneTemplateCache.ContainsKey(MundaneName))
            return $"{{=eLocation:{{=u Unknown";

        var npc = ServerSetup.Instance.GlobalMundaneTemplateCache[MundaneName];

        if (!ServerSetup.Instance.GlobalMapCache.ContainsKey(npc.AreaID))
            return $"{{=eLocation:{{=u Unknown";

        var map = ServerSetup.Instance.GlobalMapCache[npc.AreaID];
        {
            return $"{{=eLocation:{{=u {map.Name}";
        }
    }

    private int CheckAttributePredicates(Aisling player, Dictionary<int, Tuple<bool, object>> result, int n)
    {
        result[n++] = new Tuple<bool, object>(player.ExpLevel >= ExpLevel_Required,
            $"In order to learn this secret, you must be insight {ExpLevel_Required}.");
        result[n++] = new Tuple<bool, object>(player.Str >= Str_Required,
            $"You are not strong enough. ({Str_Required} Str Required)");
        result[n++] = new Tuple<bool, object>(player.Int >= Int_Required,
            $"You are not smart enough.  ({Int_Required} Int Required)");
        result[n++] = new Tuple<bool, object>(player.Wis >= Wis_Required,
            $"You are not wise enough. ({Wis_Required} Wis Required)");
        result[n++] = new Tuple<bool, object>(player.Con >= Con_Required,
            $"You lack stamina. ({Con_Required} Con Required)");
        result[n++] = new Tuple<bool, object>(player.Dex >= Dex_Required,
            $"You are not nimble enough. ({Dex_Required} Dex Required)");
        result[n++] = new Tuple<bool, object>(player.GoldPoints >= Gold_Required,
            $"You are not wealthy enough. ({Gold_Required} Gold Required)");
        //Pill Fix - Masters can still learn old skills
        result[n++] = new Tuple<bool, object>(player.Stage >= Stage_Required,
            "You must transcend further first");
        //Pill Fix - All classes can learn Peasant skills/spells.
        result[n++] =
            new Tuple<bool, object>((player.Path == Class_Required) || (Class_Required == Class.Peasant) || (player.Path == Multiclass_Required),
                "This secret is not for you, " + player.Path);

        return n;
    }

    private int CheckItemPredicates(Aisling player, Dictionary<int, Tuple<bool, object>> result, int n)
    {
        if ((Items_Required != null) && (Items_Required.Count > 0))
        {
            var msg = new StringBuilder(ServerSetup.Instance.Config.ItemNotRequiredMsg);

            var items = Items_Required.Select(i => $"{i.Item} ({i.AmountRequired}) ");

            foreach (var itemstrs in items) msg.Append(itemstrs);

            var errorMsg = msg.ToString();

            var formatted = errorMsg.Replace(") ", "), ").TrimEnd(',', ' ');

            foreach (var ir in Items_Required)
            {
                if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(ir.Item))
                {
                    result[n] = new Tuple<bool, object>(false, formatted);

                    break;
                }

                var item = ServerSetup.Instance.GlobalItemTemplateCache[ir.Item];

                if (item == null)
                {
                    result[n] = new Tuple<bool, object>(false, formatted);
                    break;
                }

                var item_obtained = player.Inventory.Snapshot(i => i.Template.Name.EqualsI(item.Name));

                var item_total = 0;

                foreach (var itemObj in item_obtained)
                {
                    var itemcount = 0;
                    if (itemObj.Template.CanStack)
                        itemcount += itemObj.Stacks;
                    else
                        itemcount++;

                    item_total += itemcount;
                }

                if (item_total >= ir.AmountRequired)
                    result[n] = new Tuple<bool, object>(true, string.Empty);
                else
                    result[n] = new Tuple<bool, object>(false, formatted);

                n++;
            }
        }

        return n;
    }

    private int CheckSpellandSkillPredicates(Aisling player, Dictionary<int, Tuple<bool, object>> result, int n)
    {
        if ((First_Ability_Required != null) && (First_Ability_Required != $"{string.Empty}"))
        {
            var skill = ServerSetup.Instance.GlobalSkillTemplateCache[First_Ability_Required];
            var skill_retainer = player.SkillBook.Get(i => i.Template?.Name.Equals(skill.Name) ?? false)
                .FirstOrDefault();

            if (skill_retainer == null)
                result[n++] = new Tuple<bool, object>(false,
                    $"You must know how to use {First_Ability_Required} before you can learn this secret.");
            else
            {
                if ((skill_retainer != null) && (skill_retainer.Level >= First_Ability_Level_Required))
                    result[n++] = new Tuple<bool, object>(true,
                        "Skills Required:");
                else
                    result[n++] = new Tuple<bool, object>(false,
                        $"In order to learn this secret, you must first train {skill.Name} to level {First_Ability_Level_Required}.");
            }
        }

        if ((Second_Ability_Required != null) && (Second_Ability_Required != $"{string.Empty}"))
        {
            var spell = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => x.Value.Name == Second_Ability_Required);
            var spell_retainer = player.SpellBook.Spells.Where(i => (i.Value
                                                                     != null) && (i.Value.Template != null) &&
                                                                    (i.Value.Template.Name == Second_Ability_Required)).FirstOrDefault();

            if (spell_retainer.Value == null)
                result[n++] = new Tuple<bool, object>(false,
                    $"You must know how to cast {Second_Ability_Required} before you can learn this secret.");
            else if (spell_retainer.Value.Level >= Second_Ability_Level_Required)
                result[n++] = new Tuple<bool, object>(true,
                    "Spells Required:");
            else
                result[n++] = new Tuple<bool, object>(false,
                    //Pill fix - changed First_Ability_Level_Required to Second_Ability_Level_Required
                    $"In order to learn this secret, you must first train {Second_Ability_Required} to level {Second_Ability_Level_Required}.");
        }

        return n;
    }
    private int CheckSkillPredicates(Aisling player, Dictionary<int, Tuple<bool, object>> result, int n)
    {
        if ((First_Ability_Required != null) && (First_Ability_Required != $"{string.Empty}"))
        {
            var skill = ServerSetup.Instance.GlobalSkillTemplateCache[First_Ability_Required];
            var skill_retainer = player.SkillBook.Get(i => i.Template?.Name.Equals(skill.Name) ?? false)
                .FirstOrDefault();

            if (skill_retainer == null)
                result[n++] = new Tuple<bool, object>(false,
                    $"You must know how to use {First_Ability_Required} before you can learn this secret.");
            else
            {
                if ((skill_retainer != null) && (skill_retainer.Level >= First_Ability_Level_Required))
                    result[n++] = new Tuple<bool, object>(true,
                        "Skills Required:");
                else
                    result[n++] = new Tuple<bool, object>(false,
                        $"In order to learn this secret, you must first train {skill.Name} to level {First_Ability_Level_Required}.");
            }
        }

        if ((Second_Ability_Required != null) && (Second_Ability_Required != $"{string.Empty}"))
        {
            var skill = ServerSetup.Instance.GlobalSkillTemplateCache[Second_Ability_Required];
            var skill_retainer = player.SkillBook.Get(i => i.Template?.Name.Equals(skill.Name) ?? false)
                .FirstOrDefault();

            if (skill_retainer == null)
                result[n++] = new Tuple<bool, object>(false,
                    $"You must know how to use {Second_Ability_Required} before you can learn this secret.");
            else
            {
                if ((skill_retainer != null) && (skill_retainer.Level >= Second_Ability_Level_Required))
                    result[n++] = new Tuple<bool, object>(true,
                        "Skills Required:");
                else
                    result[n++] = new Tuple<bool, object>(false,
                        $"In order to learn this secret, you must first train {skill.Name} to level {Second_Ability_Level_Required}.");
            }
        }

        return n;
    }
    private int CheckSpellPredicates(Aisling player, Dictionary<int, Tuple<bool, object>> result, int n)
    {
        if ((First_Ability_Required != null) && (First_Ability_Required != $"{string.Empty}"))
        {
            var spell = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => x.Value.Name == First_Ability_Required);
            var spell_retainer = player.SpellBook.Spells.Where(i => (i.Value != null) && (i.Value.Template != null) &&
                                                                    (i.Value.Template.Name == First_Ability_Required)).FirstOrDefault();

            if (spell_retainer.Value == null)
                result[n++] = new Tuple<bool, object>(false, $"You must know how to cast {First_Ability_Required} before you can learn this secret.");
            else
            {
                if ((spell_retainer.Value != null) && (spell_retainer.Value.Level >= First_Ability_Level_Required))
                    result[n++] = new Tuple<bool, object>(true,
                        "Spells Required:");
                else
                    result[n++] = new Tuple<bool, object>(false,
                        $"In order to learn this secret, you must first train {First_Ability_Required} to level {First_Ability_Level_Required}.");
            }
        }

        if ((Second_Ability_Required != null) && (Second_Ability_Required != $"{string.Empty}"))
        {
            var spell = ServerSetup.Instance.GlobalSpellTemplateCache.Where(x => x.Value.Name == Second_Ability_Required);
            var spell_retainer = player.SpellBook.Spells.Where(i => (i.Value
                                                                     != null) && (i.Value.Template != null) &&
                                                                    (i.Value.Template.Name == Second_Ability_Required)).FirstOrDefault();

            if (spell_retainer.Value == null)
                result[n++] = new Tuple<bool, object>(false,
                    $"You must know how to cast {Second_Ability_Required} before you can learn this secret.");
            else if (spell_retainer.Value.Level >= Second_Ability_Level_Required)
                result[n++] = new Tuple<bool, object>(true,
                    "Spells Required:");
            else
                result[n++] = new Tuple<bool, object>(false,
                    //Pill fix - changed First_Ability_Level_Required to Second_Ability_Level_Required
                    $"In order to learn this secret, you must first train {Second_Ability_Required} to level {Second_Ability_Level_Required}.");
        }

        return n;
    }
}