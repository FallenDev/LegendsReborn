using Darkages.Common;
using Darkages.Network.Client;

namespace Darkages.Templates;

public enum TriggerType
{
    ItemDrop,
    UserClick,
    MapRandom,
    MapLocation,
    UserGossip,
    ItemPickup,
    ItemOnUse,
    SkillOnUse,
    SpellOnUse
}

public class ItemClickPopup : PopupTemplate
{
    public ItemClickPopup() => TypeOfTrigger = TriggerType.ItemOnUse;

    public bool ConsumeItem { get; set; }
    public string ItemTemplateName { get; set; }
}

public class ItemDropPopup : PopupTemplate
{
    public ItemDropPopup() => TypeOfTrigger = TriggerType.ItemDrop;

    public string ItemName { get; set; }
}

public class ItemPickupPopup : PopupTemplate
{
    public ItemPickupPopup() => TypeOfTrigger = TriggerType.ItemPickup;

    public string ItemName { get; set; }
}

public class Popup
{
    private static readonly HashSet<Popup> _popups = [];

    public Popup()
    {
        Users = [];

        lock (Generator.Random)
            Id = Generator.GenerateNumber();
    }

    public static List<Popup> Popups
    {
        get
        {
            var tmpl = new List<Popup>(_popups).ToList();

            return tmpl;
        }
        set => throw new NotImplementedException();
    }

    public int Id { get; set; }

    public uint Owner { get; set; }

    public PopupTemplate Template { get; set; }

    public List<uint> Users { get; set; }

    public static void Add(Popup obj) => _popups.Add(obj);

    public static Popup Create(WorldClient client, PopupTemplate template)
    {
        var popup = new Popup
        {
            Template = template,
            Owner = client.Aisling.Serial
        };

        var users = client.Aisling.AislingsNearby().Where(i => i.Serial != client.Aisling.Serial);
        popup.Users = [..users.Select(i => i.Serial)];

        Add(popup);

        return popup;
    }

    public static Popup Get(Predicate<Popup> predicate) => Popups.Find(predicate);

    public static Popup GetById(uint id) => Get(i => i.Id == id);

    public static void Remove(Popup obj) => _popups.RemoveWhere(i => i.Id == obj.Id);
}

public class PopupTemplate : Template
{
    public bool Ephemeral { get; set; }
    public ushort SpriteId { get; set; }
    public int Timeout { get; set; }
    public TriggerType TypeOfTrigger { get; set; }
    public string YamlKey { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];
}

public class UserClickPopup : PopupTemplate
{
    public UserClickPopup() => TypeOfTrigger = TriggerType.UserClick;

    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class UserWalkPopup : PopupTemplate
{
    public UserWalkPopup() => TypeOfTrigger = TriggerType.MapLocation;

    public int MapId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}