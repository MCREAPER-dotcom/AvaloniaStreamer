using System.Net.WebSockets;

public class StreamHub
{
    private readonly ConnectionManager _connectionManager;
    private readonly ILogger<StreamHub> _logger;
    private readonly bool _echoToSender;

    public StreamHub(ConnectionManager connectionManager,
                   ILogger<StreamHub> logger,
                   IConfiguration config)
    {
        _echoToSender = config.GetValue<bool>("Streaming:EchoToSender");
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task HandleConnectionAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var connId = _connectionManager.AddConnection(ws);

        _logger.LogInformation("New connection: {ConnectionId} (Total: {Count})",
            connId, _connectionManager.ActiveConnections);

        try
        {
            var buffer = new byte[4 * 1024 * 1024];

            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        return;

                    case WebSocketMessageType.Binary:
                        _logger.LogInformation("Received {Count} bytes from {ConnectionId}",
                            result.Count, connId);
                        await BroadcastToAllAsync(buffer, result.Count, connId);
                        break;

                    case WebSocketMessageType.Text:
                        _logger.LogInformation("Text message from {ConnectionId}", connId);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in connection {ConnectionId}", connId);
        }
        finally
        {
            _connectionManager.RemoveConnection(connId);
            await SafeCloseAsync(ws);
            _logger.LogInformation("Connection closed: {ConnectionId} (Remaining: {Count})",
                connId, _connectionManager.ActiveConnections);
        }
    }
    //// для локалки
    //private async Task BroadcastToAllAsync(byte[] buffer, int count, Guid senderId)
    //{
    //    // Убрали исключение отправителя для локального тестирования
    //    var connections = _connectionManager.GetActiveConnections(null).ToList();
    //    _logger.LogInformation("Broadcasting to {Count} clients", connections.Count);

    //    var tasks = connections.Select(conn =>
    //        SendDataAsync(conn.Socket, buffer, count, conn.Id));

    //    await Task.WhenAll(tasks);
    //}
    private async Task BroadcastToAllAsync(byte[] buffer, int count, Guid senderId)
    {
        var connections = _connectionManager
            .GetActiveConnections(_echoToSender ? null : senderId)
            .ToList();
        //var connections = _connectionManager.GetActiveConnections(senderId).ToList();
        _logger.LogInformation("Broadcasting to {Count} clients", connections.Count);

        var tasks = connections.Select(conn =>
            SendDataAsync(conn.Socket, buffer, count, conn.Id));

        await Task.WhenAll(tasks);
    }

    private async Task SendDataAsync(WebSocket ws, byte[] buffer, int count, Guid connId)
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
            _logger.LogError(ex, "Send error to {ConnectionId}", connId);
        }
    }

    private static async Task SafeCloseAsync(WebSocket ws)
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
