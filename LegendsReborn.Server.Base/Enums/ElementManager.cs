namespace Darkages.Enums;

public class ElementManager
{
    public enum Element
    {
        None = 0x00,
        Fire = 0x01,
        Water = 0x02,
        Wind = 0x03,
        Earth = 0x04,
        Light = 0x05,
        Dark = 0x06,
        Random = 0x07,
        Neutral = 0x08
    }

    public static string ElementValue(Element e)
    {
        return e switch
        {
            Element.None => "None",
            Element.Fire => "Fire",
            Element.Water => "Water",
            Element.Wind => "Wind",
            Element.Earth => "Earth",
            Element.Light => "Light",
            Element.Dark => "Dark",
            Element.Random => "Random",
            Element.Neutral => "Neutral",
            _ => "None"
        };
    }
}