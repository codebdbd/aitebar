using AiteBar;
using Xunit;

namespace AiteBar.Tests;

public sealed class ActionExecutionResultTests
{
    [Fact]
    public void Ok_ReturnsTrueAndEmptyError()
    {
        var result = ActionExecutionResult.Ok;
        Assert.True(result.Success);
        Assert.Equal("", result.ErrorMessage);
    }

    [Fact]
    public void Failed_ReturnsFalseAndErrorMessage()
    {
        var result = ActionExecutionResult.Failed("Test error");
        Assert.False(result.Success);
        Assert.Equal("Test error", result.ErrorMessage);
    }

    [Fact]
    public void Failed_WithNullMessage_UsesDefault()
    {
        var result = ActionExecutionResult.Failed(null);
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void Failed_WithEmptyMessage_UsesDefault()
    {
        var result = ActionExecutionResult.Failed("");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void Failed_WithWhitespaceMessage_UsesDefault()
    {
        var result = ActionExecutionResult.Failed("   ");
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }
}
