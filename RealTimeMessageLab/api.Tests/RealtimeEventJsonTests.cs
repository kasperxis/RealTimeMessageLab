using RealtimeMessageLab.Api;
using Xunit;

namespace RealtimeMessageLab.Api.Tests;

public class RealtimeEventJsonTests
{
    [Fact]
    public void Serialize_UsesCamelCaseNamesForTheReactApp()
    {
        // Teaches that shared data shape can be tested without opening a WebSocket.
        var message = new Message(7, "Hello", DateTimeOffset.Parse("2026-05-08T10:00:00Z"), "Kasper");
        var realtimeEvent = new RealtimeEvent("message.created", message);

        var json = RealtimeEventJson.Serialize(realtimeEvent);

        Assert.Contains("\"type\":\"message.created\"", json);
        Assert.Contains("\"message\":", json);
        Assert.Contains("\"clientName\":\"Kasper\"", json);
        Assert.Contains("\"createdAt\":", json);
        Assert.DoesNotContain("\"CreatedAt\":", json);
    }
}
