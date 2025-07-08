using Avalonia.Media.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClientStreamer.Services
{
    public class StreamBroadcastService
    {
        private readonly IWebSocketClientService _webSocketClient;
        private readonly IScreenCaptureService _screenCapture;
        private CancellationTokenSource _cts = new();
        private bool _isBroadcasting;

        public event Action<string>? OnStatusChanged;
        public event Action<Bitmap>? OnLocalImageUpdated;

        public bool IsBroadcasting => _isBroadcasting;

        public StreamBroadcastService(
        IWebSocketClientService webSocketClient,
        IScreenCaptureService screenCapture)
        {
            _webSocketClient = webSocketClient;
            _screenCapture = screenCapture;
        }

        public async Task StartBroadcastingAsync()
        {
            if (_isBroadcasting || !_webSocketClient.IsConnected) return;

            _isBroadcasting = true;
            _cts = new CancellationTokenSource();
            OnStatusChanged?.Invoke("Broadcasting started");

            try
            {
                while (_isBroadcasting && _webSocketClient.IsConnected)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    var frame = await _screenCapture.CaptureScreenAsync(_cts.Token);
                    using var ms = new System.IO.MemoryStream();
                    frame.Save(ms);
                    await _webSocketClient.SendBinaryAsync(ms.ToArray());

                    OnLocalImageUpdated?.Invoke(frame);

                    var elapsed = sw.ElapsedMilliseconds;
                    var delay = Math.Max(0, 100 - (int)elapsed);
                    await Task.Delay(delay, _cts.Token);
                }
            }
            finally
            {
                _isBroadcasting = false;
                OnStatusChanged?.Invoke("Broadcasting stopped");
            }
        }

        public void StopBroadcasting()
        {
            _isBroadcasting = false;
            _cts.Cancel();
        }
    }
}