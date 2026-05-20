using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using RealtimeMessageLab.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The React app runs from Vite at http://localhost:5173.
// CORS allows that page to call this API with fetch().
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactDevServer", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("ReactDevServer");

// This turns on WebSocket support for requests that ask to upgrade.
app.UseWebSockets();

// Learning-only storage: data disappears when the API stops.
var messageStore = new MessageStore();
var sockets = new ConcurrentDictionary<Guid, ConnectedClient>();

app.MapGet("/messages", () =>
{
    // HTTP GET is a snapshot: the browser asks "what exists right now?"
    return Results.Ok(messageStore.GetSnapshot());
});

app.MapPost("/messages", async (CreateMessageRequest request) =>
{
    var result = messageStore.Create(request.Text, request.ClientName);

    if (!result.Success)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    var message = result.Message!;

    // HTTP POST creates the message. WebSocket then tells every open browser
    // that something changed, so they can update without polling.
    await BroadcastAsync(new RealtimeEvent("message.created", message));

    return Results.Created($"/messages/{message.Id}", message);
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // Accept the browser's upgrade request and keep the connection open.
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var socketId = Guid.NewGuid();
    sockets[socketId] = new ConnectedClient(socket, new SemaphoreSlim(1, 1));

    var buffer = new byte[1024 * 4];

    try
    {
        // This sample only broadcasts server events. We still read from the
        // socket so we can notice when the browser closes the connection.
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, context.RequestAborted);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnected",
                    CancellationToken.None);
            }
        }
    }
    finally
    {
        RemoveClient(socketId);
    }
});

app.Run("http://localhost:5000");

async Task BroadcastAsync(RealtimeEvent realtimeEvent)
{
    var json = RealtimeEventJson.Serialize(realtimeEvent);
    var bytes = Encoding.UTF8.GetBytes(json);
    var closedSockets = new List<Guid>();

    foreach (var (socketId, client) in sockets)
    {
        if (client.Socket.State != WebSocketState.Open)
        {
            closedSockets.Add(socketId);
            continue;
        }

        try
        {
            await client.SendLock.WaitAsync();

            try
            {
                if (client.Socket.State == WebSocketState.Open)
                {
                    await client.Socket.SendAsync(
                        bytes,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: CancellationToken.None);
                }
                else
                {
                    closedSockets.Add(socketId);
                }
            }
            finally
            {
                client.SendLock.Release();
            }
        }
        catch
        {
            closedSockets.Add(socketId);
        }
    }

    foreach (var socketId in closedSockets)
    {
        RemoveClient(socketId);
    }
}

void RemoveClient(Guid socketId)
{
    if (sockets.TryRemove(socketId, out var client))
    {
        client.Dispose();
    }
}

record ConnectedClient(WebSocket Socket, SemaphoreSlim SendLock) : IDisposable
{
    public void Dispose()
    {
        SendLock.Dispose();
    }
}
