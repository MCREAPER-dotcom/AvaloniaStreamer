using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace ClientStreamer.Services
{
    public class WebSocketClientService : IWebSocketClientService
    {
        private ClientWebSocket _ws = new();
        private CancellationTokenSource _cts = new();
        private bool _isConnected;

        public event Action<byte[]>? OnBinaryDataReceived;
        public event Action<string>? OnStatusChanged;
        public event Action<Exception>? OnError;

        public bool IsConnected => _isConnected && _ws.State == WebSocketState.Open;

        public async Task ConnectAsync(string url)
        {
            if (_isConnected) return;

            try
            {
                ChangeStatus("Connecting...");
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(url), _cts.Token);
                _isConnected = true;
                ChangeStatus("Connected to server");

                _ = Task.Run(ConnectionKeepAlive);
                _ = Task.Run(ReceiveDataLoop);
            }
            catch (Exception ex)
            {
                ChangeStatus($"Connection failed: {ex.Message}");
                _ws?.Abort();
                _isConnected = false;
                OnError?.Invoke(ex);
            }
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected) return;

            try
            {
                _isConnected = false;
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
                ChangeStatus($"Disconnect error: {ex.Message}");
                OnError?.Invoke(ex);
            }
            finally
            {
                _ws?.Abort();
                ChangeStatus("Disconnected");
            }
        }

        public async Task SendBinaryAsync(byte[] data)
        {
            if (!IsConnected)
            {
                ChangeStatus("Cannot send - connection not open");
                return;
            }

            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Binary,
                    true,
                    _cts.Token);
            }
            catch (Exception ex)
            {
                ChangeStatus($"Send error: {ex.Message}");
                OnError?.Invoke(ex);
                throw;
            }
        }

        private async Task ConnectionKeepAlive()
        {
            while (_isConnected && _ws?.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token);
                    await _ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")),
                        WebSocketMessageType.Text,
                        true,
                        _cts.Token);
                }
                catch
                {
                    await DisconnectAsync();
                }
            }
        }

        private async Task ReceiveDataLoop()
        {
            var buffer = new byte[4 * 1024 * 1024];

            try
            {
                while (_ws?.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var data = new byte[result.Count];
                        Array.Copy(buffer, data, result.Count);
                        OnBinaryDataReceived?.Invoke(data);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        private void ChangeStatus(string status)
        {
            OnStatusChanged?.Invoke(status);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ws?.Dispose();
            _cts?.Dispose();
        }
    }
}