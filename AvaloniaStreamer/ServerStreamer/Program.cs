var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5000");

// Регистрация сервисов
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<StreamHub>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120),
    ReceiveBufferSize = 4 * 1024 * 1024
});

var hub = app.Services.GetRequiredService<StreamHub>();
app.Map("/ws", hub.HandleConnectionAsync);

Console.WriteLine("Server started at ws://localhost:5000/ws");
Console.WriteLine("Press Ctrl+C to stop");
await app.RunAsync();