using Chaos.Common.Definitions;

using Darkages.CommandSystem.CLI;
using Darkages.Network.Client;
using Darkages.Sprites;
using Darkages.Types;
using Darkages.Enums;
using static Darkages.Object.ObjectManager;

namespace Darkages.CommandSystem;

public static class Commander
{
    static Commander()
    {
        ServerSetup.Instance.Parser = CommandParser.CreateNew().UsePrefix().OnError(OnParseError);
    }

    public static void CompileCommands()
    {
        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Create Item")
            .AddAlias("give")
            .SetAction(OnItemCreate)
            .AddArgument(Argument.Create("item"))
            .AddArgument(Argument.Create("amount").MakeOptional().SetDefault(1)));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Spawn Monster")
            .AddAlias("spawn")
            .SetAction(OnMonsterCreate)
            .AddArgument(Argument.Create("name"))
            .AddArgument(Argument.Create("x"))
            .AddArgument(Argument.Create("y"))
            .AddArgument(Argument.Create("count"))
            .AddArgument(Argument.Create("direction"))
        );

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Disconnect Player")
            .AddAlias("kick")
            .SetAction(OnKick)
            .AddArgument(Argument.Create("who")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Invoke")
            .AddAlias("invoke")
            .SetAction(OnScriptInvoke)
            .AddArgument(Argument.Create("script").MakeRequired()));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Learn Skill")
            .AddAlias("skill")
            .SetAction(OnLearnSkill)
            .AddArgument(Argument.Create("name"))
            .AddArgument(Argument.Create("level").MakeOptional().SetDefault(100)));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Learn Spell")
            .AddAlias("spell")
            .SetAction(OnLearnSpell)
            .AddArgument(Argument.Create("name"))
            .AddArgument(Argument.Create("level").MakeOptional().SetDefault(100)));

        ServerSetup.Instance.Parser.AddCommand(Command
        .Create("pet")
            .AddAlias("pet")
        .SetAction(OnPet));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Port to Player")
            .AddAlias("pt")
            .SetAction(OnPortToPlayer)
        .AddArgument(Argument.Create("who")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Server Chaos")
            .AddAlias("chaos")
        .SetAction(OnChaos));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Short Reset")
            .AddAlias("reset")
        .SetAction(OnReset));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Summon Player")
            .AddAlias("sp")
            .SetAction(OnSummonPlayer)
            .AddArgument(Argument.Create("who")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Teleport")
            .AddAlias("tp")
            .SetAction(OnTeleport)
            .AddArgument(Argument.Create("map"))
            .AddArgument(Argument.Create("x"))
            .AddArgument(Argument.Create("y")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Hair Colour")
            .AddAlias("hc")
            .SetAction(OnHairdye)
            .AddArgument(Argument.Create("colour")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Hair Style")
            .AddAlias("hs")
            .SetAction(OnHairstyle)
            .AddArgument(Argument.Create("style")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Sound Test")
            .AddAlias("st")
            .SetAction(OnSoundTest)
            .AddArgument(Argument.Create("sound")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Animation Test")
            .AddAlias("fx")
            .SetAction(OnFxTest)
            .AddArgument(Argument.Create("fx")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Group Player")
            .AddAlias("g")
            .SetAction(OnGroup)
            .AddArgument(Argument.Create("who")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Creature Form")
            .AddAlias("cf")
            .SetAction(OnCreatureForm)
            .AddArgument(Argument.Create("monster")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Armor Display")
            .AddAlias("arm")
            .SetAction(OnArmorTest)
            .AddArgument(Argument.Create("armor")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Helmet Display")
            .AddAlias("hlm")
            .SetAction(OnHelmetTest)
            .AddArgument(Argument.Create("helmet")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Weapon Display")
            .AddAlias("wpn")
            .SetAction(OnWeaponTest)
            .AddArgument(Argument.Create("weapon")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Shield Display")
            .AddAlias("shd")
            .SetAction(OnShieldTest)
            .AddArgument(Argument.Create("shield")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Boots Display")
            .AddAlias("bts")
            .SetAction(OnBootsTest)
            .AddArgument(Argument.Create("boots")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("OverCoat Display")
            .AddAlias("oc")
            .SetAction(OnOverCoatTest)
            .AddArgument(Argument.Create("overcoat")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Accessory Display")
            .AddAlias("acc")
            .SetAction(OnAccessoryTest)
            .AddArgument(Argument.Create("accessory")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Chat Test")
            .AddAlias("chat")
            .SetAction(OnChatTest)
            .AddArgument(Argument.Create("hexcode"))
            .AddArgument(Argument.Create("text")));

        ServerSetup.Instance.Parser.AddCommand(Command
            .Create("Path Change")
            .AddAlias("path")
            .SetAction(OnPathChange)
            .AddArgument(Argument.Create("path")));
    }

    private static void OnPathChange(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string path = args.FromName("path").Replace("\"", "");
        if (!int.TryParse(args.FromName("path"), out var number))
        {
            client.SystemMessage("Invalid selection.");
        }
        if (client != null)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (number < 0 || number > 5)
            {
                client.SystemMessage("Valid selections are from 0-5");
                return;
            }
            switch (number)
            {
                case 0:
                    client.Aisling.Path = Class.Peasant;

                    break;
                case 1:
                    client.Aisling.Path = Class.Warrior;

                    break;
                case 2:
                    client.Aisling.Path = Class.Rogue;

                    break;
                case 3:
                    client.Aisling.Path = Class.Wizard;

                    break;
                case 4:
                    client.Aisling.Path = Class.Priest;

                    break;
                case 5:
                    client.Aisling.Path = Class.Monk;

                    break;
            }
            client.Save();
            client.Refresh();
            client.SystemMessage($"Your class has changed to {client.Aisling.Path}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} changed their path to {client.Aisling.Path} with a GM command.");
        }
    }

    private static void OnWeaponTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("weapon").Replace("\"", "");
        if (!int.TryParse(args.FromName("weapon"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 255)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 255.");
                return;
            }
            client.Aisling.WeaponImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Weapon: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed weapon {number} with a GM Command.");
        }
    }

    private static void OnHelmetTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("helmet").Replace("\"", "");
        if (!int.TryParse(args.FromName("helmet"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 32768)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 32768.");
                return;
            }
            client.Aisling.HelmetImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Helmet: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed helmet {number} with a GM Command.");
        }
    }

    private static void OnAccessoryTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string acc = args.FromName("accessory").Replace("\"", "");
        if (!int.TryParse(args.FromName("accessory"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(acc))
                return;
            if (number < 0 || number > 255)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 255.");
                return;
            }
            client.Aisling.HeadAccessoryImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Accessory: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed accessory {number} with a GM Command.");
        }
    }

    private static void OnArmorTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("armor").Replace("\"", "");
        if (!int.TryParse(args.FromName("armor"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 32768)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 32768.");
                return;
            }
            client.Aisling.ArmorImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Armor: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed armor {number} with a GM Command.");
        }
    }

    private static void OnOverCoatTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string oc = args.FromName("overcoat").Replace("\"", "");
        if (!int.TryParse(args.FromName("overcoat"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(oc))
                return;
            if (number > 0 && number < 1001 || number > 65535)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 1001 to 65535.");
                return;
            }
            client.Aisling.OverCoatImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SystemMessage($"Displaying Overcoat: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed overcoat {number} with a GM Command.");
        }
    }

    private static void OnShieldTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("shield").Replace("\"", "");
        if (!int.TryParse(args.FromName("shield"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 255)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 255.");
                return;
            }
            client.Aisling.ShieldImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Shield: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed shield {number} with a GM Command.");
        }
    }

    private static void OnBootsTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("boots").Replace("\"", "");
        if (!int.TryParse(args.FromName("boots"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 255)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 255.");
                return;
            }
            client.Aisling.BootsImg = (short)number;
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Displaying Boots: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed boots {number} with a GM Command.");
        }
    }

    private static void OnFxTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string vfx = args.FromName("fx").Replace("\"", "");
        if (!int.TryParse(args.FromName("fx"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(vfx))
                return;
            if (number < 0 || number > 65535)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 65535.");
                return;
            }
            client.SendAnimation(Convert.ToUInt16(number), null, client.Aisling.Serial);
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Showing Animation: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} displayed animation {number} with a GM Command.");
        }
    }

    private static void OnSoundTest(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string colour = args.FromName("sound").Replace("\"", "");
        if (!int.TryParse(args.FromName("sound"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(colour))
                return;
            if (number < 0 || number > 255)
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 255.");
                return;
            }
            client.SendSound(Convert.ToByte(number), false);
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Playing sound: {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} played sound effect: {number} with a GM Command.");
        }
    }

    private static void OnCreatureForm(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string colour = args.FromName("monster").Replace("\"", "");
        if (!int.TryParse(args.FromName("monster"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(colour))
                return;
            //client.Aisling.SendSound(Convert.ToByte(number),Scope.Self);
            if (number > 0 && number <= 1034)
            {
                client.Aisling.MonsterForm = (ushort)(16384 + number);
                client.SendServerMessage(ServerMessageType.ActiveMessage, $"Became Creature {number}.");
            }
            else
            {
                client.Aisling.MonsterForm = 0;
                client.SystemMessage("Valid selections range from 1 - 1034");
            }
            client.Save();
            client.UpdateDisplay();
            ServerSetup.EventsLogger($"{client.Aisling.Username} became creature {number} with a GM Command.");
        }
    }

    private static void OnGroup(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        if (client != null)
        {
            var who = args.FromName("who").Replace("\"", "");
            if (string.IsNullOrEmpty(who))
                return;
            var player = ServerSetup.Instance.Game.Aislings.FirstOrDefault(i => i != null && i.Username.ToLower() == who.ToLower()));
            if (player == null)
            {
                client.SendServerMessage(ServerMessageType.OrangeBar2, "That person is nowhere to be found.");
                return;
            }
            if (player.Username == client.Aisling.Username)
            {
                client.SystemMessage("You ask yourself to group, but get declined.");
                return;
            }
            if (player.PartyStatus == GroupStatus.AcceptingRequests)
                Party.AddPartyMember(client.Aisling, player);
            else
                client.SendServerMessage(ServerMessageType.ActiveMessage, $"{player.Username} does not wish to join a party.");
        }
    }

    private static void OnHairdye(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string colour = args.FromName("colour").Replace("\"", "");
        if (!int.TryParse(args.FromName("colour"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection.");
            return;
        }

        if (client != null)
        {
            if (string.IsNullOrEmpty(colour))
                return;
            if (number < 0 || number > 127)
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Valid selections range from 0 to 127.");
            client.Aisling.HairColor = Convert.ToByte(number);
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Hair color changed to {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} changed their hair colour to {number} with a GM Command.");
        }
    }

    private static void OnHairstyle(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        string style = args.FromName("style").Replace("\"", "");

        if (!int.TryParse(args.FromName("style"), out var number))
        {
            client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid selection");
            return;
        }
        if (client != null)
        {
            if (string.IsNullOrEmpty(style))
                return;
            if (number < 0 || number > 100)
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Not a valid hairstyle.");
            if (number < 61)
                client.Aisling.HairStyle = (ushort)number;
            else
            {
                switch (number)
                {
                    case 61:
                        client.Aisling.HairStyle = 253;

                        break;
                    case 62:
                        client.Aisling.HairStyle = 254;

                        break;
                    case 63:
                        client.Aisling.HairStyle = 255;

                        break;
                    case 64:
                        client.Aisling.HairStyle = 263;

                        break;
                    case 65:
                        client.Aisling.HairStyle = 264;

                        break;
                    case 66:
                        client.Aisling.HairStyle = 265;

                        break;
                    case 67:
                        client.Aisling.HairStyle = 266;

                        break;
                    case 68:
                        client.Aisling.HairStyle = 314;

                        break;
                    case 69:
                        client.Aisling.HairStyle = 313;

                        break;
                    case 70:
                        client.Aisling.HairStyle = 324;

                        break;
                    case 71:
                        client.Aisling.HairStyle = 327;

                        break;
                    case 72:
                        client.Aisling.HairStyle = 321;

                        break;
                    case 73:
                        client.Aisling.HairStyle = 326;

                        break;
                    case 74:
                        client.Aisling.HairStyle = 325;

                        break;
                    case 75:
                        client.Aisling.HairStyle = 333;

                        break;
                    case 76:
                        client.Aisling.HairStyle = 344;

                        break;
                    case 77:
                        client.Aisling.HairStyle = 346;

                        break;
                    case 78:
                        client.Aisling.HairStyle = 342;

                        break;
                    case 79:
                        client.Aisling.HairStyle = 343;

                        break;
                    case 80:
                        client.Aisling.HairStyle = 345;

                        break;
                    case 81:
                        client.Aisling.HairStyle = 347;

                        break;
                    case 82:
                        client.Aisling.HairStyle = 349;

                        break;
                    case 83:
                        client.Aisling.HairStyle = 383;

                        break;
                    case 84:
                        client.Aisling.HairStyle = 392;

                        break;
                    case 85:
                        client.Aisling.HairStyle = 397;

                        break;
                    case 86:
                        client.Aisling.HairStyle = 411;

                        break;
                    case 87:
                        client.Aisling.HairStyle = 412;

                        break;
                    case 88:
                        client.Aisling.HairStyle = 433;

                        break;
                    case 89:
                        client.Aisling.HairStyle = 435;

                        break;
                    case 90:
                        client.Aisling.HairStyle = 437;

                        break;
                    case 91:
                        client.Aisling.HairStyle = 438;

                        break;
                    case 92:
                        client.Aisling.HairStyle = 440;

                        break;
                    case 93:
                        client.Aisling.HairStyle = 447;

                        break;
                    case 94:
                        client.Aisling.HairStyle = 459;

                        break;
                    case 95:
                        client.Aisling.HairStyle = 460;

                        break;
                    case 96:
                        client.Aisling.HairStyle = 449;

                        break;
                    case 97:
                        client.Aisling.HairStyle = 461;

                        break;
                    case 98:
                        client.Aisling.HairStyle = 482;

                        break;
                    case 99:
                        client.Aisling.HairStyle = 483;
                        break;
                }
            }
            client.Save();
            client.UpdateDisplay();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"Hair style changed to {number}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} changed their hair style to {number} with a GM Command.");
        }
    }

    private static void OnKick(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        var who = args.FromName("who").Replace("\"", "");
        if (client != null)
        {
            if (string.IsNullOrEmpty(who)) return;
            var player = ServerSetup.Instance.Game.Aislings.FirstOrDefault(i => i != null && i.Username.ToLower() == who.ToLower());
            player.Client.SendServerMessage(ServerMessageType.ActiveMessage, $"A member of the Legends Team has disconnected you from the server.");
            player.Client.Disconnect();
            client.SendServerMessage(ServerMessageType.ActiveMessage, $"{player.Username} has been disconnected from the server.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} kicked {player.Username} from the server with a GM Command.");
        }

    }

    private static void OnPet(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client == null)
            return;

        client.Aisling.SummonObjects = new Pet(client);
        client.Aisling.SummonObjects.Spawn("Gog", "Common Pet", 2000, 75);
    }

    private static void OnScriptInvoke(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client == null)
            return;

        var script = args.FromName("script").Replace("\"", "");

        if (!string.IsNullOrEmpty(script))
        {
            var scriptObj = GetObjects<Mundane>(null, i => (i.Scripts != null) && (i.Scripts.Count > 0))
                .SelectMany(i => i.Scripts).FirstOrDefault(i => i.Key == script);

            scriptObj.Value?.OnClick(client, client.Aisling.Serial);
        }
    }

    private static void OnChaos(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        ServerSetup.EventsLogger($"{client.Aisling.Username} initiated a reset with a GM Command.");
        if (client == null)
            return;
        var players = ServerSetup.Instance.Game.Aislings;

        foreach (var connectedClients in players)
            connectedClients.Chaos = true;

        foreach (var connected in players)
        {
            connected.Client.SendServerMessage(ServerMessageType.ActiveMessage, "{=q((Chaos will rise in 5 minutes.))");
            connected.Client.SendServerMessage(ServerMessageType.ActiveMessage, "{=qPlease begin moving to a safe area.");
        }

        Task.Delay(300000).ContinueWith(_ =>
        {
            foreach (var connected in ServerSetup.Instance.Game.Aislings)
            {
                connected.Client.Save();
                connected.Client.SendServerMessage(ServerMessageType.ScrollWindow, "{=qChaos has risen.\n\n{=a((During this time, various updates may be performed by the Legends Team. Please check the Discord for the current Server Status.))\n\n-Legends Team");
                connected.Client.Disconnect();
            }
            var UTCtime = DateTime.UtcNow;
            var MyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(UTCtime, "Eastern Standard Time");
            ServerSetup.EventsLogger($"Reset occurred at {MyTime} EST. - Aisling files have been saved.");
            Console.WriteLine($"Chaos has risen at {MyTime} EST.");
            ServerSetup.Instance.Running = false;
        });
    }

    private static void OnReset(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        ServerSetup.EventsLogger($"{client.Aisling.Username} initiated a reset with a GM Command.");
        if (client == null)
            return;
        var clients = ServerSetup.Instance.Game.Clients.ToArray();

        foreach (var connectedClients in ServerSetup.Instance.Game.Clients.Where(i => (i?.Aisling != null) && (i.Chaos == false)))
            connectedClients.Chaos = true;

        foreach (var connected in clients)
        {
            connected.SendServerMessage(ServerMessageType.ActiveMessage, "{=q((Chaos will rise in 1 minute.))");
            connected.SendServerMessage(ServerMessageType.ActiveMessage, "{=qPlease move to a safe area.");
        }

        Task.Delay(60000).ContinueWith(_ =>
        {
            foreach (var connected in from client2 in ServerSetup.Instance.Game.Clients where (client2 != null) && (client2.Aisling != null) select client2)
            {
                connected.Save();
                connected.SendMessage(0x08, "{=qChaos has risen.\n\n{=a((This was done to address minor issues, and the server should be back up momentarily.\nPlease see the server discord channel for more information.))\n\n-Legends Team");
            }
            var UTCtime = DateTime.UtcNow;
            var MyTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(UTCtime, "Eastern Standard Time");
            ServerSetup.EventsLogger($"Quick reset occurred at {MyTime} EST. - Aisling files have been saved.");
            Console.WriteLine($"Server Reset at {MyTime} EST.");
            ServerSetup.Instance.Shutdown();
        });
    }

    private static void OnLearnSpell(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client == null)
            return;

        var name = args.FromName("name").Replace("\"", "");
        //var level = args.FromName("level");
        var spell = Spell.GiveTo(client.Aisling, name);
        client.SystemMessage(spell ? $"Learned {name}." : $"Failed to learn {name}.");
        ServerSetup.EventsLogger($"{client.Aisling.Username} learned {name} with a GM Command.");

        client.Save();
        client.LoadSpellBook();
        client.SendAttributes(StatUpdateType.Full);
    }

    private static void OnLearnSkill(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client == null)
            return;

        var name = args.FromName("name").Replace("\"", "");

        var level = args.FromName("level");

        if (int.TryParse(level, out var spellLevel))
        {
            var skill = Skill.GiveTo(client.Aisling, name, spellLevel);

            client.SystemMessage(skill ? $"Learned {name} Lev: {spellLevel}." : $"Failed to learn {name}.");
            ServerSetup.EventsLogger($"{client.Aisling.Username} attempted to learn {name} with a GM Command.");

        }
        else
            client.SystemMessage($"Unable to learn {name}.");

        client.Save();
        client.LoadSkillBook();
        client.SendAttributes(StatUpdateType.Full);
    }

    private static void OnMonsterCreate(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;
        if (client == null) return;
        var name = args.FromName("name").Replace("\"", "");

        if (!int.TryParse(args.FromName("x"), out var x) ||
            !int.TryParse(args.FromName("y"), out var y))
            return;

        if (!int.TryParse(args.FromName("count"), out var count))
            count = 1;

        if (!int.TryParse(args.FromName("direction"), out var direction))
            direction = 0;

        client.Spawn(name, x, y, count, direction);
        ServerSetup.EventsLogger($"{client.Aisling.Username} used a GM Command to spawn {name} ({count}) on {client.Aisling.CurrentMapId} at {x}, {y}.");
    }

    private static void OnSummonPlayer(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client != null)
        {
            var who = args.FromName("who").Replace("\"", "");

            if (string.IsNullOrEmpty(who))
                return;

            var player = client.Server.Clients.FirstOrDefault(i =>
                (i?.Aisling != null) && (i.Aisling.Username.ToLower() == who.ToLower()));

            //summon player to my map and position.
            player?.TransitionToMap(client.Aisling.Map, client.Aisling.Position);
            ServerSetup.EventsLogger($"{client.Aisling.Username} used a GM Command to summon {who}.");
        }
    }

    private static void OnPortToPlayer(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client != null)
        {
            var who = args.FromName("who").Replace("\"", "");

            if (string.IsNullOrEmpty(who))
                return;

            var player = client.Server.Clients.FirstOrDefault(i =>
                (i?.Aisling != null) && (i.Aisling.Username.ToLower() == who.ToLower()));

            //summon myself to players area and position.
            if (player != null)
                client.TransitionToMap(player.Aisling.Map, player.Aisling.Position);
            ServerSetup.EventsLogger($"{client.Aisling.Username} used a GM Command to approach {who}.");
        }
    }

    private static void OnTeleport(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client != null)
        {
            var mapName = args.FromName("map").Replace("\"", "");
            if (!int.TryParse(args.FromName("map"), out var number))
            {
                client.SendServerMessage(ServerMessageType.ActiveMessage, "Invalid Map ID");
                return;
            }

            if (!int.TryParse(args.FromName("x"), out var x) ||
                !int.TryParse(args.FromName("y"), out var y))
                return;

            var area = ServerSetup.Instance.GlobalMapCache.FirstOrDefault(i => i.Value.ID == number);

            if (area.Value != null)
            {
                client.TransitionToMap(area.Value, new Position(x, y));
                ServerSetup.EventsLogger($"{client.Aisling.Username} used a GM Command to teleport to {area.Value}.");
            }
        }
    }

    private static void OnItemCreate(Argument[] args, object arg)
    {
        var client = (WorldClient)arg;

        if (client != null)
        {
            var name = args.FromName("item").Replace("\"", "");
            if (int.TryParse(args.FromName("amount"), out var quantity))
                if (client.Aisling.GiveManyItems(name, quantity))
                    ServerSetup.EventsLogger($"{client.Aisling.Username} used a GM Command to create {name} ({quantity}).");
        }
    }

    public static void ParseChatMessage(WorldClient client, string message) => ServerSetup.Instance.Parser?.Parse(message, client);

    private static void OnParseError(object obj, string command) => ServerSetup.EventsLogger($"[Chat Parser] Error: {command}");
}