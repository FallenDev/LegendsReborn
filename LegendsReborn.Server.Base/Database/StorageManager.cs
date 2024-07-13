using Darkages.Templates;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Numerics;

namespace Darkages.Database;

public static class StorageManager
{
    //Pill Fix - Clan Bucket
    public static TemplateStorage<ClanTemplate> ClanBucket = new();
    //Pill Fix - Parcel Bucket
    public static TemplateStorage<ParcelTemplate> ParcelBucket = new();
    public static readonly AislingStorage AislingBucket = new();
    public static readonly TemplateStorage<ItemTemplate> ItemBucket = new();
    public static readonly TemplateStorage<MonsterTemplate> MonsterBucket = new();
    public static readonly TemplateStorage<MundaneTemplate> MundaneBucket = new();
    public static readonly TemplateStorage<NationTemplate> NationBucket = new();
    public static readonly TemplateStorage<PopupTemplate> PopupBucket = new();
    public static readonly TemplateStorage<ServerTemplate> ServerArgBucket = new();
    public static readonly TemplateStorage<SkillTemplate> SkillBucket = new();
    public static readonly TemplateStorage<SpellTemplate> SpellBucket = new();
    public static readonly WarpStorage WarpBucket = new();
    public static readonly TemplateStorage<WorldMapTemplate> WorldMapBucket = new();
    private static readonly KnownTypesBinder HadesTypesBinder = new();

    static StorageManager() =>
        HadesTypesBinder.KnownTypes = new List<Type>
        {
            typeof(Reactor),
            typeof(WorldMapTemplate),
            typeof(ServerTemplate),
            typeof(PopupTemplate),
            typeof(UserClickPopup),
            typeof(ItemDropPopup),
            typeof(ItemPickupPopup),
            typeof(UserWalkPopup)
        };

    private class KnownTypesBinder : ISerializationBinder
    {
        public IList<Type> KnownTypes { get; set; }

        public Type BindToType(string assemblyName, string typeName) => KnownTypes.Distinct().SingleOrDefault(t => t.Name == typeName);

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }
    }

    public static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full,
        SerializationBinder = HadesTypesBinder,
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    public static T Deserialize<T>(string data) => JsonConvert.DeserializeObject<T>(data, Settings);
}