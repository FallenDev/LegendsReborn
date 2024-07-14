using Darkages.Network.Client;
using Darkages.Sprites;

namespace Darkages.Collections.Helpers;

public class ItemDisplay : IDisplay<Item>
{
    private WorldClient Client { get; }

    public ItemDisplay(WorldClient client) => Client = client;
    
    public void Display(Item obj) => Client.Send(new ServerFormat0F(obj));

    public void Remove(byte slot) => Client.Send(new ServerFormat10(slot));
}