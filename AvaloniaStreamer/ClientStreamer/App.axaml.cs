using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ClientStreamer.Services;
using ClientStreamer.ViewModels;
using ClientStreamer.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ClientStreamer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }
        public override void OnFrameworkInitializationCompleted()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // –егистраци€ сервисов через интерфейсы
                    services.AddSingleton<IWebSocketClientService, WebSocketClientService>();
                    services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();

                    // явна€ регистраци€ StreamBroadcastService
                    services.AddSingleton<StreamBroadcastService>(provider =>
                        new StreamBroadcastService(
                            provider.GetRequiredService<IWebSocketClientService>(),
                            provider.GetRequiredService<IScreenCaptureService>()));

                    services.AddTransient<ClientViewModel>();
                    services.AddTransient<MainWindow>();
                })
                .Build();

            ServiceProvider = host.Services;

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = ServiceProvider.GetRequiredService<ClientViewModel>();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        public static IServiceProvider? ServiceProvider { get; private set; }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}