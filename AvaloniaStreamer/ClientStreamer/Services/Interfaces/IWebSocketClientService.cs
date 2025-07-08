using System.Threading.Tasks;
using System;

public interface IWebSocketClientService : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(string url);
    Task DisconnectAsync();
    Task SendBinaryAsync(byte[] data);
    event Action<byte[]> OnBinaryDataReceived;
    event Action<string> OnStatusChanged;
}