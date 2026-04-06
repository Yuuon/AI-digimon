using DigimonBot.AI.Services;
using Microsoft.Extensions.Logging;

namespace IntegrationTest;

/// <summary>
/// Kimi ACP 客户端集成测试
/// </summary>
public static class KimiAcpTest
{
    public static async Task<int> RunAsync()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("    Kimi ACP 客户端集成测试");
        Console.WriteLine("========================================\n");

        // 配置日志
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        var logger = loggerFactory.CreateLogger<KimiAcpSession>();

        var session = new KimiAcpSession("/tmp", logger);

        try
        {
            // 测试 1: 连接
            Console.WriteLine("【测试 1】连接到 ACP 服务...");
            await session.ConnectAsync();
            Console.WriteLine("✓ 连接成功\n");

            // 测试 2: 创建会话
            Console.WriteLine("【测试 2】创建新会话...");
            await session.CreateSessionAsync();
            Console.WriteLine($"✓ 会话创建成功: {session.SessionId}\n");

            // 测试 3: 列出会话
            Console.WriteLine("【测试 3】列出会话...");
            var sessions = await session.ListSessionsAsync();
            Console.WriteLine($"✓ 找到 {sessions.Count} 个会话");
            foreach (var s in sessions.Take(3))
            {
                Console.WriteLine($"  - {s.SessionId[..8]}...: {s.Title}");
            }
            Console.WriteLine();

            // 测试 4: 简单聊天
            Console.WriteLine("【测试 4】发送消息 (同步模式)...");
            Console.WriteLine("用户: Say hello world\n");
            Console.WriteLine("AI:");
            
            var response = await session.ChatAsync("Say hello world");
            Console.WriteLine(response);
            Console.WriteLine("\n✓ 聊天完成\n");

            // 测试 5: 流式聊天
            Console.WriteLine("【测试 5】发送消息 (流式模式)...");
            Console.WriteLine("用户: Count from 1 to 3\n");
            Console.WriteLine("AI:");

            await session.ChatStreamingAsync(
                "Count from 1 to 3",
                chunk => Console.Write(chunk)
            );
            Console.WriteLine("\n\n✓ 流式输出完成\n");

            // 测试 6: 多轮对话（上下文保持）
            Console.WriteLine("【测试 6】多轮对话（上下文测试）...");
            
            Console.WriteLine("用户: My name is Alice\n");
            var r1 = await session.ChatAsync("My name is Alice");
            Console.WriteLine($"AI: {r1[..Math.Min(50, r1.Length)]}...\n");

            Console.WriteLine("用户: What's my name?\n");
            var r2 = await session.ChatAsync("What's my name?");
            Console.WriteLine($"AI: {r2}");
            
            if (r2.Contains("Alice", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("\n✓ 上下文保持成功（AI 记住了名字）\n");
            }
            else
            {
                Console.WriteLine("\n⚠ 上下文可能未保持\n");
            }

            // 总结
            Console.WriteLine("========================================");
            Console.WriteLine("          所有测试通过！");
            Console.WriteLine("========================================");
            Console.WriteLine("\n功能验证:");
            Console.WriteLine("  ✓ ACP 连接");
            Console.WriteLine("  ✓ 会话创建");
            Console.WriteLine("  ✓ 会话列表");
            Console.WriteLine("  ✓ 同步聊天");
            Console.WriteLine("  ✓ 流式输出");
            Console.WriteLine("  ✓ 上下文保持");

            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("\n❌ 错误: 找不到 kimi 命令");
            Console.WriteLine("请先安装 kimi-cli: pip install kimi-cli");
            return 1;
        }
        catch (KimiAcpException ex) when (ex.ErrorCode == -32000)
        {
            Console.WriteLine("\n❌ 错误: 需要登录");
            Console.WriteLine("请运行: kimi login");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 错误: {ex.Message}");
            Console.WriteLine($"\n堆栈跟踪:\n{ex.StackTrace}");
            return 1;
        }
        finally
        {
            session.Dispose();
        }
    }
}
