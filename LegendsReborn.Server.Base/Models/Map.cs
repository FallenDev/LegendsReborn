using Darkages.Database;
using Darkages.Enums;
using Darkages.Object;
using Darkages.Types;

using Newtonsoft.Json;

namespace Darkages.Models;

public class Map : ObjectManager
{
    public byte Width { get; set; }
    public string ContentName { get; set; }
    public MapFlags Flags { get; set; }
    public int ID { get; set; }
    public int Music { get; set; }
    public string Name { get; set; }
    public byte Height { get; set; }
    public List<Position> Blocks { get; set; }
    public string ScriptKey { get; set; }
    public Map() => Blocks = new List<Position>();

    public static Map Load(string name)
    {
        var path = Path.Combine($@"{ServerSetup.Instance.StoragePath}\areas\", $"{name.ToLower()}");

        if (!File.Exists(path)) 
            return null;

        using var s = File.OpenRead(path);
        using var f = new StreamReader(s);
        var content = f.ReadToEnd();
        var settings = StorageManager.Settings;
        settings.TypeNameHandling = TypeNameHandling.None;

        try
        {
            var obj = JsonConvert.DeserializeObject<Area>(content, settings);

            return obj;
        }
        catch (Exception ex)
        {
            ServerContext.Logger(ex.Message);
            ServerContext.Logger(ex.StackTrace);

            return null;
        }
    }
}

public class DiscoveredMap
{
    public int Serial { get; init; }
    public int MapId { get; init; }
}