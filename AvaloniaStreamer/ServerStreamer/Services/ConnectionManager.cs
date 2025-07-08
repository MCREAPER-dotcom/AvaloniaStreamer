using System.Net.WebSockets;
using System.Collections.Concurrent;

public class ConnectionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();

    public int ActiveConnections => _connections.Count;

    public Guid AddConnection(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _connections.TryAdd(id, socket);
        return id;
    }

    public bool RemoveConnection(Guid id)
    {
        return _connections.TryRemove(id, out _);
    }

    public IEnumerable<(Guid Id, WebSocket Socket)> GetActiveConnections(Guid? excludeId = null)
    {
        return _connections
            .Where(c => c.Value.State == WebSocketState.Open &&
                        (!excludeId.HasValue || c.Key != excludeId.Value))
            .Select(c => (c.Key, c.Value));
    }
}
