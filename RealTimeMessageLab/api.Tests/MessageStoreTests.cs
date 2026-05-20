using RealtimeMessageLab.Api;
using Xunit;

namespace RealtimeMessageLab.Api.Tests;

public class MessageStoreTests
{
    [Fact]
    public void Create_TrimsTextAndAssignsIds()
    {
        // Teaches that pure C# logic can be tested without running the web server.
        var store = new MessageStore();

        var first = store.Create("  Hello  ", "  Kasper  ");
        var second = store.Create("World");

        Assert.True(first.Success);
        Assert.Equal(1, first.Message!.Id);
        Assert.Equal("Hello", first.Message.Text);
        Assert.Equal("Kasper", first.Message.ClientName);

        Assert.True(second.Success);
        Assert.Equal(2, second.Message!.Id);
        Assert.Equal("World", second.Message.Text);
        Assert.Null(second.Message.ClientName);
    }

    [Fact]
    public void Create_RejectsBlankText()
    {
        // Teaches that validation rules are easier to test when they live outside Program.cs.
        var store = new MessageStore();

        var result = store.Create("   ");

        Assert.False(result.Success);
        Assert.Null(result.Message);
        Assert.Equal("Message text is required.", result.Error);
    }

    [Fact]
    public void GetSnapshot_ReturnsCreatedMessages()
    {
        // Teaches that tests can check state changes directly in a small class.
        var store = new MessageStore();

        store.Create("One");
        store.Create("Two");

        var snapshot = store.GetSnapshot();

        Assert.Collection(
            snapshot,
            message => Assert.Equal("One", message.Text),
            message => Assert.Equal("Two", message.Text));
    }
}
