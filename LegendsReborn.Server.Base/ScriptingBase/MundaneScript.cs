using Darkages.Interfaces;
using Darkages.Network.Client;
using Darkages.Network.Server;
using Darkages.Object;
using Darkages.Sprites;

namespace Darkages.ScriptingBase;

public abstract class MundaneScript : ObjectManager, IScriptBase
{
    protected MundaneScript(WorldServer server, Mundane mundane)
    {
        Server = server;
        Mundane = mundane;
    }
 
    public Mundane Mundane { get; set; }

    public WorldServer Server { get; set; }

    public abstract void OnClick(WorldServer server, WorldClient client);

    public abstract void OnGossip(WorldServer server, WorldClient client, string message);

    public abstract void OnResponse(WorldServer server, WorldClient client, ushort responseId, string args);

    public abstract void TargetAcquired(Sprite target);

    public virtual void OnDropped(WorldClient client, byte itemSlot)
    {

    }
}