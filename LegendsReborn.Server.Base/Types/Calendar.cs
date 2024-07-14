using System.Text.Json.Serialization;

namespace Darkages.Types;
public class Calendar
{
    private readonly long Ticks;
    private readonly DateTime DateTime;
    public int Year => DateTime.Year;
    public int Month => DateTime.Month;
    public int Day => DateTime.Day;
    public int Hour => DateTime.Hour;
    public int Minute => DateTime.Minute;
    public string Season { get; set; }

    [JsonConstructor]
    private Calendar(long ticks)
    {
        DateTime = new DateTime(ticks);
        Ticks = ticks;
    }

    private string GetSeasonSuffix => 
          (Month >=12 || Month <=2) ? "Winter"
        : (Month >=3 && Month <=5) ? "Spring"
        : (Month >=6 && Month <=8) ? "Summer"
        : "Fall";
    private string GetDaySuffix => 
              (Day % 10 == 1 && Day != 11) ? "st"
            : (Day % 10 == 2 && Day != 12) ? "nd"
            : (Day % 10 == 3 && Day != 13) ? "rd"
            : "th";
    private string GetMoon => 
        (Day / 10 == 1) ? "1st" :
        (Day / 10 == 2) ? "2nd" :
        (Day / 10 == 3) ? "3rd" : 
        "4th";
    private static DateTime Origin => new(2023, 4, 28);
    internal static Calendar Now => FromDateTime(DateTime.Now);
    internal static Calendar FromDateTime(DateTime dTime) => new(dTime.Subtract(Origin).Ticks * 8);
    internal DateTime ToDateTime() => new((Ticks / 8) + Origin.Ticks);
    //Prints the in-game date in Legend Mark format (Deoch <Year>, <Season>)
    public string LegendToString(string? format = null) => $@"Deoch {(!string.IsNullOrEmpty(format) ? DateTime.ToString(format) : DateTime.ToString($@"%y"))}, {GetSeasonSuffix}";
    //Prints the full in-game date.
    public string FullDateToString(string? format = null) => $@"Deoch {(!string.IsNullOrEmpty(format) ? DateTime.ToString(format) : DateTime.ToString(@"%y"))}, " +
        $@"{GetMoon} Moon, {(!string.IsNullOrEmpty(format) ? DateTime.ToString(format) : DateTime.ToString(@"%d"))}{GetDaySuffix} Sun, " +
        $@"{DateTime.ToString(@"%h:mmtt").ToLower()}";
}
