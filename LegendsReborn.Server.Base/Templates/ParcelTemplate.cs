using Darkages.Database;
using Darkages.Sprites;
namespace Darkages.Templates;

public class ParcelTemplate : Template
{
    public string SenderName { get; set; }
    public string ItemName { get; set; }
    public string ReceiverName { get; set; }
    public uint gold { get; set; }
    public int Amount { get; set; }
    public int Weight { get; set; }
    public DateTime Date { get; set; }

    public override string[] GetMetaData() =>
    [
        ""
    ];

    public static ParcelTemplate Create(string recipient, string sender, string item, int weight)
    {
        var result = new ParcelTemplate
        {
            Name = string.Empty,
            ReceiverName = recipient,
            SenderName = sender,
            gold = 0,
            Amount = 1,
            ItemName = item,
            Weight = weight,
            Date = DateTime.UtcNow,

        };
        String s = $"{sender.ToLower()}_{recipient.ToLower()}_{item.ToLower()}_{result.Date}";
        s = s.Replace("/", "_").Replace(" ", "_").Replace(":", "_");
        result.Name = s;
        ServerSetup.Instance.GlobalParcelTemplateCache.Add(s, result);
        StorageManager.ParcelBucket.SaveParcel(result, sender, recipient, item, result.Date, false);
        return result;
    }
    public static ParcelTemplate SendMany(string recipient, string sender, string item, int weight, int amount)
    {
        var result = new ParcelTemplate
        {
            Name = string.Empty,
            ReceiverName = recipient,
            SenderName = sender,
            gold = 0,
            Amount = amount,
            ItemName = item,
            Weight = weight,
            Date = DateTime.UtcNow,

        };
        String s = $"{sender.ToLower()}_{recipient.ToLower()}_{item.ToLower()}_{result.Date}";
        s = s.Replace("/", "_").Replace(" ", "_").Replace(":", "_");
        result.Name = s;
        ServerSetup.Instance.GlobalParcelTemplateCache.Add(s, result);
        StorageManager.ParcelBucket.SaveParcel(result, sender, recipient, item, result.Date, false);
        return result;
    }
    public static ParcelTemplate ReceiveItems(Aisling playername)
    {
        var receivedparcel = new ParcelTemplate();
        var parcels = ServerSetup.Instance.GlobalParcelTemplateCache.Values;
        string sourcedir = $@"{ServerSetup.Instance.StoragePath}\templates\Parcels";

        foreach (var parcel in from parcel in parcels where (parcel != null) && (parcel.ReceiverName.ToLower() == playername.Username.ToLower()) select parcel)
            if (playername.Client.Aisling.CurrentWeight + parcel.Weight <= playername.Client.Aisling.MaximumWeight)
            {
                playername.GiveManyItems(parcel.ItemName, parcel.Amount);

                try
                {
                    ServerSetup.Instance.GlobalParcelTemplateCache.Remove(parcel.Name);
                    string[] piclist = Directory.GetFiles(sourcedir, $"{parcel.Name}.json");
                    foreach (string f in piclist)
                        File.Delete(f);
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"{playername} was unable to receive their parcel due to an error.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }

        return receivedparcel;
    }

}