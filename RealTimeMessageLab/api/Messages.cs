using System.Text.Json;

namespace RealtimeMessageLab.Api;

public record CreateMessageRequest(string Text, string? ClientName);

public record Message(int Id, string Text, DateTimeOffset CreatedAt, string? ClientName);

public record RealtimeEvent(string Type, Message Message);

public record CreateMessageResult(bool Success, Message? Message, string? Error);

public class MessageStore
{
    private readonly List<Message> messages = [];
    private readonly object gate = new();
    private int nextMessageId = 1;

    public Message[] GetSnapshot()
    {
        lock (gate)
        {
            return [.. messages];
        }
    }

    public CreateMessageResult Create(string? text, string? clientName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new CreateMessageResult(false, null, "Message text is required.");
        }

        var trimmedClientName = string.IsNullOrWhiteSpace(clientName) ? null : clientName.Trim();

        lock (gate)
        {
            var message = new Message(
                Id: nextMessageId++,
                Text: text.Trim(),
                CreatedAt: DateTimeOffset.UtcNow,
                ClientName: trimmedClientName);

            messages.Add(message);

            return new CreateMessageResult(true, message, null);
        }
    }
}

public static class RealtimeEventJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(RealtimeEvent realtimeEvent)
    {
        return JsonSerializer.Serialize(realtimeEvent, JsonOptions);
    }
}
