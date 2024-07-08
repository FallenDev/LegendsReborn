using System;
using System.Reflection;
using Chaos.Networking.Options;
using Darkages;
using Darkages.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsReborn;

public interface IServer
{
    string LegendsVersion { get; }
}

public class Server : IServer
{
    public Server(ILogger<ServerSetup> logger, IServerContext context, IServerConstants configConstants,
        IOptions<ServerOptions> serverOptions)
    {
        var time = DateTime.UtcNow;
        var localLogger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (serverOptions.Value.Location == null) return;

        context.InitFromConfig(serverOptions.Value.Location, serverOptions.Value.ServerIp);
        localLogger.LogInformation($"{configConstants.SERVER_TITLE}: Server Version: {LegendsVersion}. Server IP: {serverOptions.Value.ServerIp} Started: {time}");
        context.Start(configConstants, logger);
    }

    public string LegendsVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? version.ToString() : string.Empty;
        }
    }
}