using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Tests;

public class EmailQueueValidationTests
{
    [Fact]
    public void Empty_required_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => EmailQueue.NormalizeAddressList(Array.Empty<string>(), "To", required: true));
        Assert.Throws<ArgumentException>(() => EmailQueue.NormalizeAddressList(new[] { "", "  " }, "To", required: true));
        Assert.Throws<ArgumentException>(() => EmailQueue.NormalizeAddressList(null, "To", required: true));
    }

    [Fact]
    public void Empty_optional_list_returns_null()
    {
        Assert.Null(EmailQueue.NormalizeAddressList(null, "Cc", required: false));
        Assert.Null(EmailQueue.NormalizeAddressList(new[] { " " }, "Cc", required: false));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("a@")]
    [InlineData("@b.test")]
    public void Unparseable_address_is_rejected(string bad)
    {
        Assert.Throws<ArgumentException>(() => EmailQueue.NormalizeAddressList(new[] { "ok@example.test", bad }, "To", required: true));
    }

    [Fact]
    public void Valid_addresses_are_trimmed_reduced_to_bare_address_and_joined()
    {
        var result = EmailQueue.NormalizeAddressList(new[] { " a@example.test ", "Jonas Jonaitis <b@example.test>" }, "To", required: true);

        Assert.Equal("a@example.test, b@example.test", result);
    }

    [Fact]
    public void Joined_list_longer_than_column_is_rejected()
    {
        var many = Enumerable.Range(0, 100).Select(i => $"recipient{i:D3}@example.test").ToArray();

        Assert.Throws<ArgumentException>(() => EmailQueue.NormalizeAddressList(many, "To", required: true));
    }
}
