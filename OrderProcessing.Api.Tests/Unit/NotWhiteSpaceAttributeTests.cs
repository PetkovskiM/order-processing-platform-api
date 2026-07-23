using OrderProcessing.Api.Validation;

namespace OrderProcessing.Api.Tests.Unit;

public sealed class NotWhiteSpaceAttributeTests
{
    private readonly NotWhiteSpaceAttribute _attribute = new();

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("John", true)]
    [InlineData(" Product ", true)]
    public void IsValid_ReturnsExpectedResult(
        string value,
        bool expected)
    {
        // Act
        var result = _attribute.IsValid(value);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsValid_WhenValueIsNull_ReturnsTrue()
    {
        // Act
        var result = _attribute.IsValid(null);

        // Assert
        Assert.True(result);
    }
}