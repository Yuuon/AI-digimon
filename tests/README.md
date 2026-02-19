# 测试指南

## 测试项目结构

```
tests/
├── DigimonBot.Core.Tests/       # 核心功能单元测试
│   ├── Models/
│   │   ├── EmotionValuesTests.cs
│   │   └── DigimonStageTests.cs
│   └── Services/
│       ├── EvolutionEngineTests.cs
│       └── EmotionTrackerTests.cs
├── DigimonBot.AI.Tests/         # AI功能单元测试
│   └── Services/
│       ├── AIClientFactoryTests.cs
│       └── PersonalityEngineTests.cs
├── IntegrationTestGuide.md      # 集成测试指南
└── README.md                    # 本文件
```

## 运行测试

### 1. 运行所有单元测试

```bash
# 还原测试项目依赖
dotnet restore tests/DigimonBot.Core.Tests
dotnet restore tests/DigimonBot.AI.Tests

# 运行所有测试
dotnet test

# 运行特定项目
dotnet test tests/DigimonBot.Core.Tests
dotnet test tests/DigimonBot.AI.Tests

# 详细输出
dotnet test --verbosity normal
```

### 2. 测试覆盖率

```bash
# 生成覆盖率报告
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 生成HTML报告（需要报告生成器）
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"coveragereport"
```

## 测试内容说明

### 单元测试

#### EmotionValuesTests
- 情感值的增删改查
- 匹配度计算
- 复杂度计算
- 克隆功能

#### DigimonStageTests
- 各阶段能力限制验证
- 最终形态判定

#### EvolutionEngineTests
- 进化条件判定（Token/情感）
- 多进化路径优先级
- 轮回进化逻辑
- 进度计算

#### EmotionTrackerTests
- 情感变化应用
- 上限/下限约束
- 描述生成
- 主导情感识别

#### AIClientFactoryTests
- 各提供商客户端创建
- 配置参数传递
- 默认值处理

#### PersonalityEngineTests
- 系统提示词构建
- 阶段约束生成
- 进化公告生成

### 集成测试

详见 [IntegrationTestGuide.md](./IntegrationTestGuide.md)

## 添加新测试

### 创建新的测试类

```csharp
using Xunit;
using DigimonBot.Core.Models;

namespace DigimonBot.Core.Tests.Models;

public class NewFeatureTests
{
    [Fact]
    public void TestMethod_Scenario_ExpectedResult()
    {
        // Arrange
        var input = new SomeClass();
        
        // Act
        var result = input.DoSomething();
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    public void TestMethod_MultipleCases(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}
```

## 测试最佳实践

1. **命名规范**：`TestMethod_Scenario_ExpectedResult`
2. **独立性**：每个测试应该独立运行，不依赖其他测试
3. **快速执行**：单元测试应该在毫秒级完成
4. **确定性**：同样的输入应该总是产生同样的输出

## 调试测试

在VS Code中：
1. 安装 C# Dev Kit 扩展
2. 在测试方法上点击 "Debug Test"

在Visual Studio中：
1. 打开 Test Explorer
2. 右键点击测试 → Debug

命令行调试：
```bash
dotnet test --filter "FullyQualifiedName~EvolutionEngineTests" --verbosity detailed
```
