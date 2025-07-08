using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Drawing.Imaging;
using ScreenCaptureLibrary.Platforms.Windows;
using SkiaSharp;
using System.Text;

namespace ClientStreamer.ViewModels
{
    public class ClientViewModel : ReactiveObject
    {
        private string _status = "Ready";
        private ClientWebSocket _ws = new();
        private CancellationTokenSource _cts = new();
        private Bitmap? _localImage;
        private Bitmap? _remoteImage;
        private bool _isConnected;
        private bool _isBroadcasting;
        private DateTime _lastFrameTime;

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

        public ReactiveCommand<Unit, Unit> StartBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> StopBroadcastCommand { get; }
        public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
        public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

        public ClientViewModel()
        {
            StartBroadcastCommand = ReactiveCommand.CreateFromTask(StartBroadcasting);
            StopBroadcastCommand = ReactiveCommand.Create(StopBroadcasting);
            ConnectCommand = ReactiveCommand.CreateFromTask(ConnectToServer);
            DisconnectCommand = ReactiveCommand.Create(Disconnect);

            // Обработка ошибок
            StartBroadcastCommand.ThrownExceptions.Subscribe(OnError);
            StopBroadcastCommand.ThrownExceptions.Subscribe(OnError);
            ConnectCommand.ThrownExceptions.Subscribe(OnError);
            DisconnectCommand.ThrownExceptions.Subscribe(OnError);
        }

        private async Task StartBroadcasting()
        {
            if (_isBroadcasting) return;

            _isBroadcasting = true;
            Status = "Broadcasting started";

            try
            {
                while (_isBroadcasting && _ws.State == WebSocketState.Open)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Захват и отправка кадра
                    var frame = await CaptureScreenAsync(_cts.Token);
                    await SendFrameAsync(frame);

                    // Обновление локального превью
                    await Dispatcher.UIThread.InvokeAsync(() => LocalImage = frame);

                    // Поддержание FPS (примерно 10 кадров/сек)
                    var elapsed = sw.ElapsedMilliseconds;
                    var delay = Math.Max(0, 100 - (int)elapsed);
                    await Task.Delay(delay, _cts.Token);
                }
            }
            finally
            {
                _isBroadcasting = false;
                Status = "Broadcasting stopped";
            }
        }

        private void StopBroadcasting()
        {
            _isBroadcasting = false;
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        }
        private async Task ConnectToServer()
        {
            if (_isConnected) return;

            try
            {
                Status = "Connecting...";
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri("ws://localhost:5000/ws"), _cts.Token);
                _isConnected = true;
                Status = "Connected to server";

                // Добавьте периодический ping
                _ = Task.Run(ConnectionKeepAlive);

                // Запустите прием данных
                _ = Task.Run(ReceiveDataLoop);
            }
            catch (Exception ex)
            {
                Status = $"Connection failed: {ex.Message}";
                _ws?.Abort();
                _isConnected = false;
            }
        }

        private async Task ConnectionKeepAlive()
        {
            while (_isConnected && _ws?.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30));
                    await _ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token);
                }
                catch
                {
                    Disconnect();
                }
            }
        }
        private async void Disconnect()
        {
            if (!_isConnected) return;

            try
            {
                _isConnected = false;
                _isBroadcasting = false;
                _cts.Cancel();

                if (_ws.State == WebSocketState.Open ||
                    _ws.State == WebSocketState.CloseReceived ||
                    _ws.State == WebSocketState.CloseSent)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                       "User requested",
                                       CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Status = $"Disconnect error: {ex.Message}";
            }
            finally
            {
                _ws.Abort();
                RemoteImage?.Dispose();
                RemoteImage = null;
                Status = "Disconnected";
            }
        }
        private async Task SendFrameAsync(Bitmap frame)
        {
            if (_ws.State != WebSocketState.Open)
            {
                Status = "Cannot send - connection not open";
                return;
            }
            try
            {
                using var ms = new MemoryStream();
                frame.Save(ms); // Сохраняет в PNG

                await _ws.SendAsync(
                    new ArraySegment<byte>(ms.ToArray()),
                    WebSocketMessageType.Binary,
                    true,
                    _cts.Token);

                _lastFrameTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Status = $"Send error: {ex.Message}";
                throw;
            }
        }

        private async Task ReceiveDataLoop()
        {
            var buffer = new byte[4 * 1024 * 1024];

            try
            {
                while (_ws?.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await ProcessReceivedFrame(buffer, result.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex}");
            }
            finally
            {
                Disconnect();
            }
        }
        private async Task ProcessReceivedFrame(byte[] buffer, int count)
        {
            try
            {
                using var ms = new MemoryStream(buffer, 0, count);
                var bitmap = new Bitmap(ms);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Освобождаем предыдущее изображение
                    if (RemoteImage is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    RemoteImage = bitmap;
                    Status = $"Frame received: {DateTime.Now:HH:mm:ss.fff}";

                    // Принудительное обновление (на всякий случай)
                    this.RaisePropertyChanged(nameof(RemoteImage));
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing frame: {ex}");
                await Dispatcher.UIThread.InvokeAsync(() =>
                    Status = $"Error: {ex.Message}");
            }
        }

        private async Task<Bitmap> CaptureScreenAsync(CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                // Захват экрана через System.Drawing
                using var screenBmp = WindowsScreenCapturer.CaptureScreen();

                // Конвертация в Avalonia Bitmap
                using var ms = new MemoryStream();
                screenBmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                return new Bitmap(ms);
            }, ct);
        }

        private void OnError(Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
                Status = $"Error: {ex.GetType().Name} - {ex.Message}");
        }
    }
}