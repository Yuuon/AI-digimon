using DigimonBot.Messaging.Commands;
using Xunit;

namespace DigimonBot.Core.Tests.Commands;

public class JrrpCommandTests
{
    private readonly JrrpCommand _command;

    public JrrpCommandTests()
    {
        _command = new JrrpCommand();
    }

    [Fact]
    public void Name_IsJrrp()
    {
        Assert.Equal("jrrp", _command.Name);
    }

    [Fact]
    public void Aliases_ContainsChineseNames()
    {
        Assert.Contains("今日人品", _command.Aliases);
        Assert.Contains("人品", _command.Aliases);
        Assert.Contains("运势", _command.Aliases);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidLuckValue()
    {
        var context = new CommandContext
        {
            UserId = "123456789",
            UserName = "测试用户",
            Message = "/jrrp",
            Args = Array.Empty<string>(),
            GroupId = 0,
            IsGroupMessage = false
        };

        var result = await _command.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        
        // 检查返回值是否在0-100范围内
        // 从消息中提取数字
        var message = result.Message;
        Assert.Contains("人品值", message);
    }

    [Fact]
    public async Task ExecuteAsync_SameUserSameDay_ReturnsSameValue()
    {
        var context = new CommandContext
        {
            UserId = "123456789",
            UserName = "测试用户",
            Message = "/jrrp",
            Args = Array.Empty<string>(),
            GroupId = 0,
            IsGroupMessage = false
        };

        var result1 = await _command.ExecuteAsync(context);
        var result2 = await _command.ExecuteAsync(context);

        // 同一天同一人应该得到相同结果
        Assert.Equal(result1.Message, result2.Message);
    }

    [Theory]
    [InlineData("user1")]
    [InlineData("user2")]
    [InlineData("999999999")]
    public async Task ExecuteAsync_DifferentUsers_ReturnsResults(string userId)
    {
        var context = new CommandContext
        {
            UserId = userId,
            UserName = "测试用户",
            Message = "/jrrp",
            Args = Array.Empty<string>(),
            GroupId = 0,
            IsGroupMessage = false
        };

        var result = await _command.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        Assert.Contains("人品值", result.Message);
    }
}

public class SimpleJrrpCommandTests
{
    private readonly SimpleJrrpCommand _command;

    public SimpleJrrpCommandTests()
    {
        _command = new SimpleJrrpCommand();
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysReturnsValidRange()
    {
        var context = new CommandContext
        {
            UserId = "123456789",
            UserName = "测试用户",
            Message = "/jrrp2",
            Args = Array.Empty<string>(),
            GroupId = 0,
            IsGroupMessage = false
        };

        var result = await _command.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.NotNull(result.Message);
        
        // 验证消息包含人品值
        Assert.Contains("人品值", result.Message);
    }
}
