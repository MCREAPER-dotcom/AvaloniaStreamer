using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClientStreamer.Services;

namespace ClientStreamer.ViewModels
{
    public class ClientViewModel : ReactiveObject, IDisposable
    {
        private readonly IWebSocketClientService _webSocketClient;
        private readonly StreamBroadcastService _broadcastService;
        private Bitmap? _localImage;
        private Bitmap? _remoteImage;
        private string _status = "Ready";

        public string Status
        {
            get => _status;
            private set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public Bitmap? LocalImage
        {
            get => _localImage;
            private set => this.RaiseAndSetIfChanged(ref _localImage, value);
        }

        public Bitmap? RemoteImage
        {
            get => _remoteImage;
            private set => this.RaiseAndSetIfChanged(ref _remoteImage, value);
        }

        // Изменено: Инициализация команд в объявлении
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
        public ReactiveCommand<Unit, Unit> StartBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> StopBroadcastCommand { get; }

        public ClientViewModel(
            IWebSocketClientService webSocketClient,
            StreamBroadcastService broadcastService)
        {
            _webSocketClient = webSocketClient;
            _broadcastService = broadcastService;

            // Инициализация команд
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
            DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
            StartBroadcastCommand = ReactiveCommand.CreateFromTask(StartBroadcastingAsync);
            StopBroadcastCommand = ReactiveCommand.Create(StopBroadcasting);

            SetupEventHandlers();
            SetupErrorHandling();
        }

        private void SetupEventHandlers()
        {
            _webSocketClient.OnStatusChanged += status =>
                Dispatcher.UIThread.Post(() => Status = status);

            _webSocketClient.OnBinaryDataReceived += data =>
                Dispatcher.UIThread.Post(() => ProcessReceivedFrame(data));

            _broadcastService.OnLocalImageUpdated += image =>
                Dispatcher.UIThread.Post(() => LocalImage = image);
        }

        private void SetupErrorHandling()
        {
            ConnectCommand.ThrownExceptions.Subscribe(ex =>
                Status = $"Connection error: {ex.Message}");

            StartBroadcastCommand.ThrownExceptions.Subscribe(ex =>
                Status = $"Broadcast error: {ex.Message}");
        }

        private async Task ConnectAsync()
        {
            await _webSocketClient.ConnectAsync("ws://localhost:5000/ws");
        }

        private async Task DisconnectAsync()
        {
            await _webSocketClient.DisconnectAsync();
            RemoteImage = null;
        }

        private async Task StartBroadcastingAsync()
        {
            await _broadcastService.StartBroadcastingAsync();
        }

        private void StopBroadcasting()
        {
            _broadcastService.StopBroadcasting();
        }

        private void ProcessReceivedFrame(byte[] frameData)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(frameData);
                var bitmap = new Bitmap(ms);

                RemoteImage?.Dispose();
                RemoteImage = bitmap;
                Status = $"Frame received: {DateTime.Now:HH:mm:ss.fff}";
            }
            catch (Exception ex)
            {
                Status = $"Frame error: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _webSocketClient.Dispose();
            _broadcastService.StopBroadcasting();
            RemoteImage?.Dispose();
            LocalImage?.Dispose();
        }
    }
}