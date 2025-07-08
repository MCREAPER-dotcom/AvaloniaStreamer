using System.Net.WebSockets;
using System.Collections.Concurrent;

internal class Program
{
    private static readonly ConcurrentDictionary<Guid, WebSocket> _activeConnections = new();

    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://localhost:5000");
        var app = builder.Build();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120),
            ReceiveBufferSize = 4 * 1024 * 1024
        });

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var connId = Guid.NewGuid();
            _activeConnections.TryAdd(connId, ws);

            Console.WriteLine($"[Server] New connection: {connId} (Total: {_activeConnections.Count})");

            try
            {
                var buffer = new byte[4 * 1024 * 1024];

                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        Console.WriteLine($"[Server] Received {result.Count} bytes from {connId}");
                        await BroadcastToAllClients(buffer, result.Count, connId);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        // Обработка текстовых сообщений (например, ping)
                        Console.WriteLine($"[Server] Received text message from {connId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error: {ex.Message}");
            }
            finally
            {
                _activeConnections.TryRemove(connId, out _);
                await SafeClose(ws);
                Console.WriteLine($"[Server] Connection closed: {connId} (Remaining: {_activeConnections.Count})");
            }
        });

        Console.WriteLine("Server started at ws://localhost:5000/ws");
        Console.WriteLine("Press Ctrl+C to stop");
        await app.RunAsync();
    }

    static async Task BroadcastToAllClients(byte[] buffer, int count, Guid senderId)
    {
        Console.WriteLine($"[Server] Active connections: {_activeConnections.Count}");

        int clients = 0;
        var tasks = new List<Task>();

        foreach (var (connId, ws) in _activeConnections)
        {
            if (connId != senderId /*<-убрать для локального теста*/ && ws.State == WebSocketState.Open)
            {
                clients++;
                tasks.Add(SendImage(ws, buffer, count));
                Console.WriteLine($"[Server] Sending to {connId}");
            }
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"[Server] Broadcasted to {clients} clients");
    }

    static async Task SendImage(WebSocket ws, byte[] buffer, int count)
    {
        try
        {
            await ws.SendAsync(
                new ArraySegment<byte>(buffer, 0, count),
                WebSocketMessageType.Binary,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Send error: {ex.Message}");
        }
    }

    static async Task SafeClose(WebSocket ws)
    {
        try
        {
            if (ws?.State == WebSocketState.Open)
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Closing",
                    CancellationToken.None);
            }
        }
        catch
        {
            // Игнорируем ошибки закрытия
        }
        finally
        {
            ws?.Dispose();
        }
    }
}