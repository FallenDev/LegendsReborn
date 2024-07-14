using Chaos.Common.Definitions;
using Darkages.Enums;
using Darkages.ScriptingBase;
using Darkages.Templates;
using Darkages.Types;
using Darkages.Common;
using Newtonsoft.Json;
using Chaos.Common.Identity;
using Darkages.Network.Client;

namespace Darkages.Sprites;

public sealed class Item : Sprite
{
    public Sprite[] AuthenticatedAislings { get; set; }
    public byte Color { get; set; }
    public ushort DisplayImage { get; set; }
    public string DisplayName => GetDisplayName();
    public int Durability { get; set; }
    public bool Equipped { get; set; }
    [JsonProperty] public bool Identifed { get; set; }
    public ushort Image { get; set; }
    public uint Owner { get; set; }
    [JsonIgnore] public Dictionary<string, ItemScript> Scripts { get; set; }
    public byte Slot { get; set; }
    public ushort Stacks { get; set; }
    public ItemTemplate Template { get; set; }
    //public Type Type { get; set; }
    public bool[] Warnings { get; set; }

    public uint ItemId { get; set; }
    [JsonIgnore] public Dictionary<string, WeaponScript> WeaponScripts { get; set; }
    public byte InventorySlot { get; set; }
    public string Name { get; set; }

    public static Item Create(Sprite owner, string item)
    {
        if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(item))
            return null;

        var template = ServerSetup.Instance.GlobalItemTemplateCache[item];
        return Create(owner, template);
    }

    public static Item Create(Sprite owner, ItemTemplate itemtemplate)
    {
        if (owner == null)
            return null;

        if (!ServerSetup.Instance.GlobalItemTemplateCache.ContainsKey(itemtemplate.Name))
            return null;

        var template = ServerSetup.Instance.GlobalItemTemplateCache[itemtemplate.Name] ?? itemtemplate;
        var obj = new Item
        {
            Name = template.Name,
            Serial = EphemeralRandomIdGenerator<uint>.Shared.NextId,
            ItemId = EphemeralRandomIdGenerator<uint>.Shared.NextId,
            AbandonedDate = DateTime.UtcNow,
            Template = template,
            XPos = owner.XPos,
            YPos = owner.YPos,
            Image = template.Image,
            DisplayImage = template.DisplayImage,
            CurrentMapId = owner.CurrentMapId,
            Owner = owner.Serial,
            Durability = template.MaxDurability,
            OffenseElement = template.OffenseElement,
            DefenseElement = template.DefenseElement,
            Color = (byte)template.Color,
            Warnings = [false, false, false, false],
            AuthenticatedAislings = null
        };

        if (obj.Color == 0)
            obj.Color = (byte)ServerSetup.Instance.Config.DefaultItemColor;

        if (obj.Template.Flags.HasFlag(ItemFlags.Repairable))
        {
            if (obj.Template.MaxDurability == uint.MinValue)
            {
                obj.Template.MaxDurability = ServerSetup.Instance.Config.DefaultItemDurability;
                obj.Durability = ServerSetup.Instance.Config.DefaultItemDurability;
            }

            if (obj.Template.Value == uint.MinValue)
                obj.Template.Value = ServerSetup.Instance.Config.DefaultItemValue;
        }

        if (obj.Template.Flags.HasFlag(ItemFlags.QuestRelated))
        {
            obj.Template.MaxDurability = 0;
            obj.Durability = 0;
        }

        obj.Scripts = ScriptManager.Load<ItemScript>(template.ScriptName, obj);
        if (!string.IsNullOrEmpty(obj.Template.WeaponScript))
            obj.WeaponScripts = ScriptManager.Load<WeaponScript>(obj.Template.WeaponScript, obj);

        return obj;
    }

    public void ApplyModifiers(WorldClient client)
    {
        if ((client == null) || (client.Aisling == null))
            return;

        #region Armor class modifiers

        if (Template.Acmodifier != 0)
        {
            client.Aisling.BonusAc -= Template.Acmodifier;
            client.SendAttributes(StatUpdateType.Full);
        }

        #endregion

        #region Lines
        if (Template.SpellType != 0)
        {
            int op = Template.SpellLineValue;
            int staff = Template.SpellType;
            int set = Template.SpellSetValue;

            for (var i = 0; i < client.Aisling.SpellBook.Spells.Count; i++)
            {
                var spell = client.Aisling.SpellBook.FindInSlot(i);

                if ((spell == null) || (spell.Template == null))
                    continue;

                switch (staff)
                {
                    //Kronos Rod > 2 lines = 1 line
                    //Luna Rod > 4 lines = 2 lines
                    case 1:
                        {
                            if (spell.Template.BaseLines == op)
                                spell.Lines = set;
                        }
                        break;

                    //Ares Rod > Cradh = 1 line, Ao Cradhs -1 line
                    case 2:
                        {
                            if (spell.Template.Name.ContainsIn("cradh") && (!spell.Template.Name.ContainsIn("ao")))
                                spell.Lines = set;
                            if (spell.Template.Name.ContainsIn("cradh") && spell.Template.Name.ContainsIn("ao"))
                                spell.Lines -= set;
                        }
                        break;
                    //Zeus Rod > Targetable buffs 0-line, Lamh 0 line
                    case 3:
                        {
                            if (spell.Template.Name.ContainsIn("aiseag beatha") ||
                                spell.Template.Name.ContainsIn("aiseag spiorad") ||
                                spell.Template.Name.ContainsIn("armachd") ||
                                spell.Template.Name.ContainsIn("beannaich") ||
                                spell.Template.Name.ContainsIn("fas deireas") ||
                                spell.Template.Name.ContainsIn("fas beothail") ||
                                spell.Template.Name.ContainsIn("lamh") ||
                                spell.Template.Name.ContainsIn("spionnadh"))
                            {
                                spell.Lines = set;
                            }
                        }
                        break;
                    //Demeter Rod - Elemental magic lines /2 (beag = 1, base / mor / ard = 2)
                    case 4:
                        {
                            if (spell.Template.ElementalProperty == ElementManager.Element.Earth ||
                                spell.Template.ElementalProperty == ElementManager.Element.Wind ||
                                spell.Template.ElementalProperty == ElementManager.Element.Fire ||
                                spell.Template.ElementalProperty == ElementManager.Element.Water ||
                                spell.Template.ElementalProperty == ElementManager.Element.Light)
                                spell.Lines /= set;
                        }
                        break;
                    //Athena Rod - 3 line spells = 1 line
                    case 6:
                        {
                            if (spell.Template.BaseLines == op)
                                spell.Lines = set;
                        }
                        break;
                    //Holy Diana - Heal = 0
                    case 7:
                        {
                            if ((spell.Template.Name.ContainsIn("ioc") && !spell.Template.Name.ContainsIn("bais")) ||
                                spell.Template.Name.ContainsIn("nuadhaich"))
                                spell.Lines = set;
                        }
                        break;
                    //Empowered Diana - Heal = 0, All -1 || Grandmaster Diana - Heal = 0, All -2
                    case 8:
                        {
                            spell.Lines -= set;
                            if ((spell.Template.Name.ContainsIn("ioc") && !spell.Template.Name.ContainsIn("bais")) ||
                                spell.Template.Name.ContainsIn("nuadhaich"))
                                spell.Lines = 0;
                        }
                        break;
                    //Magus Hades - All = 3
                    case 9:
                        {
                            if (spell.Template.BaseLines > set)
                                spell.Lines = set;
                        }
                        break;
                    //Empowered Hades - All = 3, All -1, Cradh 1
                    //GM - All = 2, All -1, Cradh 1
                    case 10:
                        {
                            if (spell.Template.BaseLines <= op)
                                spell.Lines -= 1;
                            if (spell.Template.BaseLines > set)
                                spell.Lines = set;
                            if (spell.Template.Name.ContainsIn("cradh") && !spell.Template.Name.ContainsIn("ao"))
                                spell.Lines = 1;
                        }
                        break;
                    //Cursed Enchanted Weapon
                    case 11:
                        {

                            if (spell.Template.Name.ContainsIn("cradh") && (!spell.Template.Name.ContainsIn("ao")))
                                spell.Lines = set;
                        }
                        break;
                }
                if (spell.Lines < spell.Template.MinLines)
                    spell.Lines = spell.Template.MinLines;

                if (client.Aisling.GameMaster)
                    spell.Lines = 0;
                UpdateSpellSlot(client, spell.Slot);
                if (spell.Template.Cooldown > 0)
                {
                    var cooldownRemaining = (DateTime.UtcNow - spell.NextAvailableUse).TotalSeconds;
                    if (cooldownRemaining < 0)
                    {
                        client.SendCooldown(false, spell.Slot, Math.Abs(Convert.ToInt32(cooldownRemaining)));
                    }
                }
            }
        }

        #endregion

        #region MR

        if (Template.Mrmodifier != 0)
        {
            if (Template.Mrmodifier > 0)
                client.Aisling.BonusMr += Template.Mrmodifier;
            if (Template.Mrmodifier < 0)
                client.Aisling.BonusMr += Template.Mrmodifier;
        }

        #endregion

        #region Health

        if (Template.Healthmodifier != 0)
        {
            if (Template.Healthmodifier > 0)
                client.Aisling.BonusHp += Template.Healthmodifier;
            if (Template.Healthmodifier < 0)
                client.Aisling.BonusHp += Template.Healthmodifier;

            if (client.Aisling.BonusHp < 0)
                client.Aisling.BonusHp = ServerSetup.Instance.Config.MinimumHp;
        }

        #endregion

        #region Mana

        if (Template.Manamodifier != 0)
        {
            if (Template.Manamodifier > 0)
                client.Aisling.BonusMp += Template.Manamodifier;
            if (Template.Manamodifier < 0)
                client.Aisling.BonusMp += Template.Manamodifier;
        }

        #endregion

        #region Regen

        if (Template.Regenmodifier != 0)
        {
            if (Template.Regenmodifier > 0)
                client.Aisling.BonusRegen += Template.Regenmodifier;
            if (Template.Regenmodifier < 0)
                client.Aisling.BonusRegen += Template.Regenmodifier;
        }

        #endregion

        #region Str

        if (Template.Strmodifier != 0)
        {
            if (Template.Strmodifier > 0)
                client.Aisling.BonusStr += Template.Strmodifier;
            if (Template.Strmodifier < 0)
                client.Aisling.BonusStr += Template.Strmodifier;
        }

        #endregion

        #region Int

        if (Template.Intmodifier != 0)
        {
            if (Template.Intmodifier > 0)
                client.Aisling.BonusInt += Template.Intmodifier;
            if (Template.Intmodifier < 0)
                client.Aisling.BonusInt += Template.Intmodifier;
        }

        #endregion

        #region Wis

        if (Template.Wismodifier != 0)
        {
            if (Template.Wismodifier > 0)
                client.Aisling.BonusWis += Template.Wismodifier;
            if (Template.Wismodifier < 0)
                client.Aisling.BonusWis += Template.Wismodifier;
        }

        #endregion

        #region Con

        if (Template.Conmodifier != 0)
        {
            if (Template.Conmodifier > 0)
                client.Aisling.BonusCon += Template.Conmodifier;
            if (Template.Conmodifier < 0)
                client.Aisling.BonusCon += Template.Conmodifier;
        }

        #endregion

        #region Dex

        if (Template.Dexmodifier != 0)
        {
            if (Template.Dexmodifier > 0)
                client.Aisling.BonusDex += Template.Dexmodifier;
            if (Template.Dexmodifier < 0)
                client.Aisling.BonusDex += Template.Dexmodifier;
        }

        #endregion

        #region Hit

        //TODO: needs to be fixed
        if (Template.Hitmodifier != 0)
        {
            if (Template.Hitmodifier > 0)
                client.Aisling.BonusHit += (byte)Template.Hitmodifier;
            if (Template.Hitmodifier < 0)
                client.Aisling.BonusHit += (byte)Template.Hitmodifier;
        }

        #endregion

        #region Dmg

        //TODO: needs to be fixed
        if (Template.Dmgmodifier != 0)
        {
            if (Template.Dmgmodifier > 0)
                client.Aisling.BonusDmg += (byte)Template.Dmgmodifier;
            if (Template.Dmgmodifier < 0)
                client.Aisling.BonusDmg += (byte)Template.Dmgmodifier;
        }

        #endregion

        //client.SendStats(StatusFlags.All);
        client.SendServerMessage(ServerMessageType.ActiveMessage, $"{Template.Name}: Ac {client.Aisling.Ac}, Mr {client.Aisling.Mr}, R {client.Aisling.Regen}");
    }

    public void Release(Sprite owner, Position position)
    {
        XPos = position.X;
        YPos = position.Y;

        CurrentMapId = owner.CurrentMapId;
        InventorySlot = 0;
        Serial = EphemeralRandomIdGenerator<uint>.Shared.NextId;

        foreach (var playerNearby in AislingsNearby())
        {
            var obj = new List<Sprite> { this };
            playerNearby.Client.SendVisibleEntities(obj);
        }

        AddObject(this);
    }

    public void RemoveModifiers(WorldClient client)
    {
        if ((client == null) || (client.Aisling == null))
            return;

        #region Armor class modifiers

        if (Template.Acmodifier != 0)
        {
            client.Aisling.BonusAc += Template.Acmodifier;
            client.SendAttributes(StatUpdateType.Full);
        }

        #endregion

        #region Lines

        if (Template.SpellType != 0)
            for (var i = 0; i < client.Aisling.SpellBook.Spells.Count; i++)
            {
                var spell = client.Aisling.SpellBook.FindInSlot(i);

                if (spell?.Template == null)
                    continue;

                spell.Lines = spell.Template.BaseLines;
                spell.ManaCost = spell.Template.ManaCost;

                if (spell.Lines > spell.Template.MaxLines)
                    spell.Lines = spell.Template.MaxLines;

                UpdateSpellSlot(client, spell.Slot);

                if (spell.Template.Cooldown > 0)
                {
                    var cooldownRemaining = (DateTime.UtcNow - spell.NextAvailableUse).TotalSeconds;
                    if (cooldownRemaining < 0)
                    {
                        client.SendCooldown(false, spell.Slot, Math.Abs(Convert.ToInt32(cooldownRemaining)));
                    }
                }
            }

        #endregion

        #region MR

        if (Template.Mrmodifier != 0)
        {
            if (Template.Mrmodifier < 0)
                client.Aisling.BonusMr -= Template.Mrmodifier;
            if (Template.Mrmodifier > 0)
                client.Aisling.BonusMr -= Template.Mrmodifier;

            //if (client.Aisling.BonusMr < 0)
            //    client.Aisling.BonusMr = 0;
        }

        #endregion

        #region Health

        if (Template.Healthmodifier != 0)
        {
            if (Template.Healthmodifier < 0)
                client.Aisling.BonusHp -= Template.Healthmodifier;
            if (Template.Healthmodifier > 0)
                client.Aisling.BonusHp -= Template.Healthmodifier;

            if (client.Aisling.BonusHp < 0)
                client.Aisling.BonusHp = ServerSetup.Instance.Config.MinimumHp;
        }

        #endregion

        #region Mana

        if (Template.Manamodifier != 0)
        {
            if (Template.Manamodifier < 0)
                client.Aisling.BonusMp -= Template.Manamodifier;
            if (Template.Manamodifier > 0)
                client.Aisling.BonusMp -= Template.Manamodifier;
        }

        #endregion

        #region Regen

        if (Template.Regenmodifier != 0)
        {
            if (Template.Regenmodifier > 0)
                client.Aisling.BonusRegen -= Template.Regenmodifier;
            if (Template.Regenmodifier < 0)
                client.Aisling.BonusRegen -= Template.Regenmodifier;
        }

        #endregion

        #region Str

        if (Template.Strmodifier != 0)
        {
            if (Template.Strmodifier > 0)
                client.Aisling.BonusStr -= Template.Strmodifier;
            if (Template.Strmodifier < 0)
                client.Aisling.BonusStr -= Template.Strmodifier;
        }

        #endregion

        #region Int

        if (Template.Intmodifier != 0)
        {
            if (Template.Intmodifier > 0)
                client.Aisling.BonusInt -= Template.Intmodifier;
            if (Template.Intmodifier < 0)
                client.Aisling.BonusInt -= Template.Intmodifier;
        }

        #endregion

        #region Wis

        if (Template.Wismodifier != 0)
        {
            if (Template.Wismodifier > 0)
                client.Aisling.BonusWis -= Template.Wismodifier;
            if (Template.Wismodifier < 0)
                client.Aisling.BonusWis -= Template.Wismodifier;
        }

        #endregion

        #region Con

        if (Template.Conmodifier != 0)
        {
            if (Template.Conmodifier > 0)
                client.Aisling.BonusCon -= Template.Conmodifier;
            if (Template.Conmodifier < 0)
                client.Aisling.BonusCon -= Template.Conmodifier;
        }

        #endregion

        #region Dex

        if (Template.Dexmodifier != 0)
        {
            if (Template.Dexmodifier > 0)
                client.Aisling.BonusDex -= Template.Dexmodifier;
            if (Template.Dexmodifier < 0)
                client.Aisling.BonusDex -= Template.Dexmodifier;
        }

        #endregion

        #region Hit

        if (Template.Hitmodifier != 0)
        {
            if (Template.Hitmodifier > 0)
                client.Aisling.BonusHit -= (byte)Template.Hitmodifier;
            if (Template.Hitmodifier < 0)
                client.Aisling.BonusHit -= (byte)Template.Hitmodifier;
        }

        #endregion

        #region Dmg

        if (Template.Dmgmodifier != 0)
        {
            if (Template.Dmgmodifier > 0)
                client.Aisling.BonusDmg -= (byte)Template.Dmgmodifier;
            if (Template.Dmgmodifier < 0)
                client.Aisling.BonusDmg -= (byte)Template.Dmgmodifier;
        }

        #endregion

        client.SendAttributes(StatUpdateType.Full);
        client.SendServerMessage(ServerMessageType.ActiveMessage, $"E: None, Ac: {client.Aisling.Ac}, Mr: {client.Aisling.Mr}, R: {client.Aisling.Regen}");
    }

    public void UpdateSpellSlot(WorldClient client, byte slot)
    {
        var a = client.Aisling.SpellBook.Remove(slot);
        client.SendRemoveSpellFromPane(slot);
        if (a != null)
        {
            a.Slot = slot;
            client.Aisling.SpellBook.Set(a, false);
            client.SendAddSpellToPane(a);
        }
    }

    private string GetDisplayName() => Template.Name;
}