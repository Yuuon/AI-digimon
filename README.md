# 数码宝贝AI Bot

一个基于C#和AI的QQ Bot，模拟数码宝贝的成长与进化。

## 功能特性

- 🥚 **进化系统**：参考《数码宝贝世界：新秩序》设计，四维情感属性（勇气、友情、爱心、知识）决定进化路线
- 🤖 **AI驱动对话**：接入DeepSeek等AI API，根据数码宝贝阶段和性格生成不同风格的回复
- 📊 **情感分析**：AI自动分析对话内容，增加相应的情感属性值
- 🔄 **轮回进化**：究极体之后会回到幼年期，开始新的旅程
- 🎮 **指令系统**：支持状态查询、进化路线预览等指令
- 🛠️ **可视化编辑器**：WPF工具方便编辑复杂的进化表

## 部署

详细部署指南请参考：
- [DEPLOY.md](DEPLOY.md) - 完整部署教程
- [NAPCAT_GUIDE.md](NAPCAT_GUIDE.md) - NapCatQQ 安装和配置指南
- [DEPLOY_CHECKLIST.md](DEPLOY_CHECKLIST.md) - 部署前检查清单

### 快速部署

```bash
# 使用部署脚本（推荐）
chmod +x deploy.sh
./deploy.sh root@your-server-ip

# 或手动部署
# 详见 DEPLOY.md
```

## 测试

在部署之前，强烈建议运行测试验证功能：

```bash
# 运行所有单元测试
dotnet test

# 运行特定模块测试
dotnet test tests/DigimonBot.Core.Tests
dotnet test tests/DigimonBot.AI.Tests

# 详细输出
dotnet test --verbosity normal
```

### 测试覆盖范围

- ✅ 情感值计算与匹配
- ✅ 进化引擎逻辑
- ✅ 阶段能力限制
- ✅ AI客户端工厂
- ✅ 人格提示词构建

### 手动集成测试

详见 [tests/IntegrationTestGuide.md](tests/IntegrationTestGuide.md)

包含：
- AI API连接测试
- 进化系统测试
- 控制台交互测试

## 快速开始

### 1. 配置环境

```bash
# 安装 .NET 8.0 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### 2. 配置API密钥

编辑 `src/DigimonBot.Host/Configs/appsettings.json`：

```json
{
  "QQBot": {
    "NapCat": {
      "ConnectionType": "WebSocketReverse",
      "WebSocketHost": "127.0.0.1",
      "WebSocketPort": 5140,
      "HttpApiUrl": "http://127.0.0.1:3000"
    }
  },
  "AI": {
    "Provider": "deepseek",
    "ApiKey": "your-api-key-here",
    "Model": "deepseek-chat",
    "BaseUrl": null,
    "TimeoutSeconds": 60,
    "Temperature": 0.8,
    "MaxTokens": 1000
  }
}
```

### 3. 安装并配置 NapCatQQ

NapCatQQ 需要单独安装和配置。请参考：
- [NAPCAT_GUIDE.md](NAPCAT_GUIDE.md) - NapCatQQ 安装和配置指南
- [NapCatQQ 官方文档](https://napneko.github.io/)

#### 快速启动 NapCatQQ

1. 下载并安装 NapCatQQ（参考官方文档）
2. 配置 NapCatQQ 的 `onebot11` 配置项：

```json
{
  "network": {
    "websocket_reverse": [
      {
        "enable": true,
        "url": "ws://127.0.0.1:5140/onebot"
      }
    ],
    "http": [
      {
        "enable": true,
        "host": "127.0.0.1",
        "port": 3000
      }
    ]
  }
}
```

### 支持的AI提供商

| 提供商 | Provider值 | 推荐模型 | 获取API Key |
|--------|-----------|---------|------------|
| **DeepSeek** | `deepseek` | `deepseek-chat` | https://platform.deepseek.com/ |
| **智谱GLM** | `glm` | `glm-4-flash` (免费) | https://open.bigmodel.cn/ |
| **OpenAI兼容** | `openai` | 根据服务商 | 根据服务商 |
| **自定义** | `custom` | 自定义 | 自定义 |

### 配置示例

**使用DeepSeek（默认）：**
```json
"AI": {
  "Provider": "deepseek",
  "ApiKey": "sk-xxxxxxxx",
  "Model": "deepseek-chat"
}
```

**使用智谱GLM（免费版）：**
```json
"AI": {
  "Provider": "glm",
  "ApiKey": "xxxxxxxx.xxxxxxxx",
  "Model": "glm-4-flash"
}
```

**使用硅基流动（国内DeepSeek）：**
```json
"AI": {
  "Provider": "openai",
  "ApiKey": "sk-xxxxxxxx",
  "Model": "deepseek-ai/DeepSeek-V2.5",
  "BaseUrl": "https://api.siliconflow.cn/v1"
}
```

### 配置参数说明

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `Provider` | AI提供商类型 | `deepseek` |
| `ApiKey` | API密钥 | 必填 |
| `Model` | 模型名称 | 根据提供商 |
| `BaseUrl` | 自定义API地址 | 提供商默认 |
| `TimeoutSeconds` | 请求超时时间 | 60 |
| `Temperature` | 创造性参数(0-2) | 0.8 |
| `MaxTokens` | 最大Token数 | 1000 |

### 4. 运行Bot

确保 NapCatQQ 已启动并登录成功后，运行 Bot：

```bash
# 编译并运行
dotnet run --project src/DigimonBot.Host

# 或使用发布模式
dotnet publish src/DigimonBot.Host -c Release -o ./publish
./publish/DigimonBot.Host.exe
```

连接成功后控制台会显示 `Connected to NapCatQQ WebSocket successfully!`

## 项目结构

```
DigimonBot/
├── src/
│   ├── DigimonBot.Core/      # 核心领域模型和服务接口
│   │   ├── Models/            # 数码宝贝、情感、进化等模型
│   │   ├── Services/          # 进化引擎、情感追踪器
│   │   └── Events/            # 事件定义
│   ├── DigimonBot.AI/        # AI相关服务
│   │   └── Services/          # DeepSeek客户端、人格引擎
│   ├── DigimonBot.Data/      # 数据层
│   │   └── Repositories/      # JSON仓库、内存管理器
│   ├── DigimonBot.Messaging/ # 消息处理
│   │   ├── Commands/          # 指令系统
│   │   └── Handlers/          # 消息处理器
│   └── DigimonBot.Host/      # 宿主程序
│       └── Configs/           # 配置文件
├── tools/
│   └── EvolutionEditor/       # 进化表编辑工具 (WPF)
└── Data/
    └── digimon_database.json  # 数码宝贝数据库
```

## 指令列表

| 指令 | 别名 | 说明 |
|------|------|------|
| `/status` | 状态, s | 查看当前数码宝贝状态 |
| `/path` | 进化路线, p | 查看可能的进化路线 |
| `/reset` | 重置, r | 重置数码宝贝，从蛋开始 |
| `/jrrp` | 今日人品, 运势 | 查看今日人品值 |
| `/setemotion` | 设置情感, emotion | 【管理员】修改情感值（白名单限定） |
| `/help` | 帮助, ? | 显示帮助信息 |

### 管理指令说明

`/setemotion` 指令用于手动调整情感值，仅限白名单用户使用：

```bash
# 增加/减少情感值
/setemotion courage 10      # 勇气+10
/setemotion love -5         # 爱心-5

# 直接设置情感值
/setemotion courage=50      # 设置勇气为50

# 查看当前情感值
/setemotion show

# 重置所有情感值
/setemotion reset
```

**配置白名单**：在 `appsettings.json` 的 `Admin.Whitelist` 中添加QQ号：

```json
{
  "Admin": {
    "Whitelist": ["你的QQ号", "好友QQ号"]
  }
}
```

## 进化系统详解

### 四维情感属性

- **勇气 (Courage)**：主动、挑战、保护行为
- **友情 (Friendship)**：陪伴、合作、关心
- **爱心 (Love)**：温柔、治愈、体贴
- **知识 (Knowledge)**：学习、探索、智慧

### 进化条件

每个进化选项需要满足：
1. **Token消耗**：累计消耗的API token数量达到阈值
2. **情感属性**：当前情感值满足要求

### 进化优先级

当满足多个进化条件时：
1. 复杂度更高（涉及更多情感属性）优先
2. 优先级字段数值高者优先
3. 匹配度最高者优先

### 轮回系统

究极体和超究极体进化后：
- 返回幼年期I（蛋状态）
- Token计数重置
- 情感属性重置
- 开始新的成长旅程

## 使用编辑器

```bash
# 运行进化表编辑器
dotnet run --project tools/EvolutionEditor
```

编辑器功能：
- 可视化编辑数码宝贝属性
- 拖拽式配置进化路线
- 实时JSON预览
- 搜索和筛选功能

## 部署到云服务器

### 1. 发布程序

```bash
dotnet publish src/DigimonBot.Host -c Release -r linux-x64 --self-contained true -o ./publish
```

### 2. 上传到服务器

```bash
scp -r ./publish user@your-server:/opt/digimon-bot/
```

### 3. 使用 systemd 管理

创建 `/etc/systemd/system/digimon-bot.service`：

```ini
[Unit]
Description=Digimon QQ Bot
After=network.target

[Service]
Type=simple
User=bot
WorkingDirectory=/opt/digimon-bot
ExecStart=/opt/digimon-bot/DigimonBot.Host
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

启动服务：

```bash
sudo systemctl enable digimon-bot
sudo systemctl start digimon-bot
sudo journalctl -u digimon-bot -f
```

## 自定义数码宝贝

编辑 `Data/digimon_database.json`，添加新的数码宝贝定义：

```json
{
  "id": "mydigimon",
  "name": "我的数码宝贝",
  "stage": "Child",
  "personality": "Brave",
  "appearance": "描述外观",
  "basePrompt": "系统提示词...",
  "nextEvolutions": [
    {
      "targetId": "evolution_target",
      "requirements": {
        "courage": 30,
        "friendship": 20,
        "love": 0,
        "knowledge": 0
      },
      "minTokens": 10000,
      "priority": 1,
      "description": "进化描述"
    }
  ]
}
```

## 常见问题

**Q: 为什么重启后数码宝贝重置了？**  
A: 这是设计特性。数码宝贝生命周期是「重启即重置」，符合数码世界轮回的设定。

**Q: 可以对接其他AI API吗？**  
A: 可以。DeepSeekClient实现了OpenAI兼容接口，可以替换为其他API（如智谱GLM）。

**Q: 群聊中如何触发Bot？**  
A: 需要在消息中@Bot，或发送以`/`开头的指令。

**Q: NapCatQQ 和 Bot 必须运行在同一台机器上吗？**  
A: 不需要。只要网络可达，NapCatQQ 和 Bot 可以运行在不同的服务器上。只需配置正确的 WebSocket 和 HTTP 地址即可。

## 技术栈

- **框架**: .NET 8, NapCatQQ (OneBot11协议)
- **AI**: DeepSeek API (OpenAI兼容)
- **数据**: JSON配置文件
- **编辑器**: WPF (.NET 8)

## 许可证

MIT License

---

🌟 如果觉得项目有用，请给个Star！
