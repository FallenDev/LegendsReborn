#region

using Darkages.Templates;
using Newtonsoft.Json;

#endregion

namespace Darkages.Database;

public class WarpStorage : IStorage<WarpTemplate>
{
    private static readonly string StoragePath;

    static WarpStorage()
    {
        StoragePath = $@"{ServerSetup.Instance.StoragePath}\templates\warps";

        if (!Directory.Exists(StoragePath))
            Directory.CreateDirectory(StoragePath);
    }

    public static void CacheFromStorage()
    {
        var areaDir = StoragePath;
        if (!Directory.Exists(areaDir)) return;
        var areaNames = Directory.GetFiles(areaDir, "*.json", SearchOption.TopDirectoryOnly);

        foreach (var area in areaNames)
        {
            var obj = StorageManager.WarpBucket.Load(Path.GetFileNameWithoutExtension(area));
            ServerSetup.Instance.GlobalWarpTemplateCache.Add(obj);
        }
    }

    public WarpTemplate Load(string name)
    {
        var path = Path.Combine(StoragePath, $"{name.ToLower()}.json");

        if (!File.Exists(path)) return null;

        using var s = File.OpenRead(path);
        using var f = new StreamReader(s);
        return JsonConvert.DeserializeObject<WarpTemplate>(f.ReadToEnd(), StorageManager.Settings);
    }

    public WarpTemplate LoadAisling(string name) => throw new NotImplementedException();

    public bool Save<TA>(WarpTemplate obj)
    {
        var path = Path.Combine(StoragePath, $"{obj.Name.ToLower()}.json");
        var objString = JsonConvert.SerializeObject(obj, StorageManager.Settings);

        using var writer = File.CreateText(path);
        writer.Write(objString);
        writer.Close();

        return true;
    }
}