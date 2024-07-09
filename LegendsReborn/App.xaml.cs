using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Chaos.Extensions.Common;
using Chaos.Extensions.DependencyInjection;
using Chaos.Networking;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities;
using Chaos.Networking.Options;
using Darkages;
using Darkages.Interfaces;
using Darkages.Network.Client;
using Darkages.Network.Client.Abstractions;
using Darkages.Network.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LegendsReborn;

public partial class App
{
    private CancellationTokenSource ServerCtx { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += GlobalUnhandledException;

        base.OnStartup(e);
        ServerCtx = new CancellationTokenSource();
        var providers = new LoggerProviderCollection();
        const string logTemplate = "[{Timestamp:MMM-dd HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("_Legends_logs_.txt", LogEventLevel.Verbose, logTemplate, rollingInterval: RollingInterval.Day)
            .WriteTo.Console(LogEventLevel.Verbose, logTemplate, theme: AnsiConsoleTheme.Literate)
            .CreateLogger();

        Win32.AllocConsole();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("LegendsConfig.json");

        try
        {
            var config = builder.Build();
            var constants = config.GetSection("LegendsConfig").Get<ServerConstants>();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions()
                .AddSingleton(providers)
                .AddSingleton<ILoggerFactory>(sc =>
                {
                    var providerCollection = sc.GetService<LoggerProviderCollection>();
                    var factory = new SerilogLoggerFactory(null, true, providerCollection);

                    foreach (var provider in sc.GetServices<ILoggerProvider>())
                        factory.AddProvider(provider);

                    return factory;
                })
                .AddLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Trace))
                .Configure<ServerOptions>(config.GetSection("Content"));
            serviceCollection.AddSingleton<IServerConstants, ServerConstants>(_ => constants)
                .AddSingleton<IServerContext, ServerSetup>()
                .AddSingleton<IServer, Server>();
            serviceCollection.AddCryptography();
            serviceCollection.AddPacketSerializer();
            serviceCollection.AddSingleton<IRedirectManager, RedirectManager>();

            // Lobby
            serviceCollection.AddSingleton<IClientFactory<LobbyClient>, ClientFactory<LobbyClient>>();
            serviceCollection.AddSingleton<ILobbyServer<LobbyClient>, IHostedService, LobbyServer>();
            serviceCollection.AddSingleton<IClientRegistry<ILobbyClient>, ClientRegistry<ILobbyClient>>();

            // Login
            serviceCollection.AddSingleton<IClientFactory<LoginClient>, ClientFactory<LoginClient>>();
            serviceCollection.AddSingleton<ILoginServer<LoginClient>, IHostedService, LoginServer>();
            serviceCollection.AddSingleton<IClientRegistry<ILoginClient>, ClientRegistry<ILoginClient>>();

            // World
            serviceCollection.AddSingleton<IClientFactory<WorldClient>, ClientFactory<WorldClient>>();
            serviceCollection.AddSingleton<IWorldServer<WorldClient>, IHostedService, WorldServer>();
            serviceCollection.AddSingleton<IClientRegistry<IWorldClient>, ClientRegistry<IWorldClient>>();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            serviceProvider.GetService<IServer>();

            var hostedServices = serviceProvider.GetServices<IHostedService>().ToArray();
            await Task.Run(async () =>
            {
                await Task.WhenAll(hostedServices.Select(svc => svc.StartAsync(ServerCtx.Token)));
                await ServerCtx.Token.WaitTillCanceled().ConfigureAwait(false);
            });
        }
        catch (Exception exception)
        {
            Log.Logger.Debug(exception, exception.ToString());
        }
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Logger.Debug(e.Exception, e.Exception.ToString());
        e.Handled = true;
    }

    private static void GlobalUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.IsTerminating)
        {
            Log.Logger.Debug(e.ExceptionObject as Exception ?? throw new InvalidOperationException(), e.ExceptionObject.ToString() ?? "Unable to follow exception");
        }
        else
        {
            Log.Logger.Debug(e.ExceptionObject as Exception, e.ExceptionObject.ToString() ?? "Unable to follow exception");
        }
    }
}