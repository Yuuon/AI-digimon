# 数码宝贝AI Bot

一个基于C#和AI的QQ Bot，模拟数码宝贝的成长与进化。

## 功能特性

- 🥚 **进化系统**：参考《数码宝贝世界：新秩序》设计，四维情感属性（勇气、友情、爱心、知识）决定进化路线
- 🤖 **AI驱动对话**：接入DeepSeek等AI API，根据数码宝贝阶段和性格生成不同风格的回复
- 📊 **情感分析**：AI自动分析对话内容，增加相应的情感属性值
- 🔄 **轮回进化**：究极体之后会回到幼年期，开始新的旅程
- 👥 **群聊模式**：支持各自培养（每人一只）或共同培养（全群一只）两种模式
- 🎭 **酒馆系统**：支持 SillyTavern 角色卡，可进行角色扮演对话和自主发言
- 🎮 **指令系统**：支持状态查询、进化路线预览、商店、背包等指令
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
      "BotQQ": 123456789,
      "ConnectionType": "WebSocketReverse",
      "WebSocketHost": "127.0.0.1",
      "WebSocketPort": 5140,
      "HttpApiUrl": "http://127.0.0.1:3000",
      "GroupDigimonMode": "Separate"
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
  },
  "Data": {
    "DigimonDatabasePath": "Data/digimon_database.json"
  },
  "Admin": {
    "Whitelist": ["你的QQ号"]
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

#### NapCatQQ 配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `BotQQ` | Bot的QQ号（用于识别@Bot的消息，**必填**） | 0 |
| `ConnectionType` | 连接方式：`WebSocketReverse`/`HTTP` | `WebSocketReverse` |
| `WebSocketHost` | WebSocket监听地址 | `127.0.0.1` |
| `WebSocketPort` | WebSocket监听端口 | 5140 |
| `HttpApiUrl` | NapCat HTTP API地址 | `http://127.0.0.1:3000` |
| `GroupDigimonMode` | 群聊模式：`Separate`（各自培养）/`Shared`（共同培养） | `Separate` |

#### AI 配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `Provider` | AI提供商类型 | `deepseek` |
| `ApiKey` | API密钥 | 必填 |
| `Model` | 模型名称 | 根据提供商 |
| `BaseUrl` | 自定义API地址 | 提供商默认 |
| `TimeoutSeconds` | 请求超时时间 | 60 |
| `Temperature` | 创造性参数(0-2) | 0.8 |
| `MaxTokens` | 最大Token数 | 1000 |

#### 识图配置（可选）

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `VisionModel.BaseUrl` | 识图模型API地址 | 空（禁用识图） |
| `VisionModel.Model` | 识图模型名称 | 空 |
| `VisionModel.ApiKey` | 识图API密钥（可选，默认使用主AI密钥） | 空 |

**识图配置示例（智谱GLM-4.6V）：**
```json
{
  "AI": {
    "Provider": "glm",
    "ApiKey": "your-api-key",
    "Model": "glm-4",
    "VisionModel": {
      "BaseUrl": "https://open.bigmodel.cn/api/paas/v4/chat/completions",
      "Model": "glm-4.6v"
    }
  }
}
```

#### 管理配置

| 参数 | 说明 | 默认值 |
|------|------|--------|
| `Whitelist` | 管理员QQ号列表（可使用 `/setemotion` 指令） | `[]` |

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

### 群聊模式说明

`GroupDigimonMode` 配置项控制群聊中数码宝贝的养成方式：

| 模式 | 说明 | 回复格式 |
|------|------|----------|
| `Separate` | **各自培养**（默认） | 每个群友有自己的数码兽，回复带前缀 `[昵称]的XX兽：` |
| `Shared` | **共同培养** | 全群共同培养一只数码兽，回复不带前缀 |

**Separate 模式示例：**
```
[小明]的亚古兽：你好呀！
[小红]的亚古兽：今天天气不错！
```

**Shared 模式示例：**
```
亚古兽：大家好！
```

## 项目结构

```
DigimonBot/
├── src/
│   ├── DigimonBot.Core/      # 核心领域模型和服务接口
│   │   ├── Models/            # 数码宝贝、情感、进化、物品等模型
│   │   ├── Services/          # 进化引擎、情感追踪器
│   │   └── Events/            # 事件定义
│   ├── DigimonBot.AI/        # AI相关服务
│   │   └── Services/          # DeepSeek客户端、人格引擎、酒馆服务
│   ├── DigimonBot.Data/      # 数据层
│   │   ├── Database/          # 数据库初始化器
│   │   └── Repositories/      # JSON仓库、SQLite仓库、持久化管理器
│   ├── DigimonBot.Messaging/ # 消息处理
│   │   ├── Commands/          # 指令系统（状态、商店、背包、酒馆等）
│   │   └── Handlers/          # 消息处理器
│   └── DigimonBot.Host/      # 宿主程序
│       └── Configs/           # 配置文件
├── tools/
│   └── EvolutionEditor/       # 进化表编辑工具 (WPF)
└── Data/
    ├── digimon_database.json  # 数码宝贝数据库
    ├── items_database.json    # 物品数据库
    ├── bot_data.db            # SQLite 用户数据数据库
    └── Characters/            # 酒馆角色卡目录
```

## 指令列表

| 指令 | 别名 | 说明 |
|------|------|------|
| `/status` | 状态, s | 查看数码宝贝状态（可加QQ号/@他人查看他人数据） |
| `/path` | 进化路线, p | 查看进化路线（可加QQ号/@他人查看他人数据） |
| `/reset` | 重置, r | 重置数码宝贝，从蛋开始 |
| `/attack` | 攻击, a, fight | 命令数码兽攻击目标（@用户 或 物体描述） |
| `/checkin` | 签到, sign, 打卡 | 每日签到获得奖励并与数码宝贝互动 |
| `/whatisthis` | 这是什么, 识图, img | 识别最近消息中的图片内容 |
| `/jrrp` | 今日人品, 运势 | 查看今日人品值 |
| `/setemotion` | 设置情感, emotion | 【管理员】修改情感值（白名单限定） |
| `/shop` | 商店, buy | 查看商店商品或购买物品 |
| `/inventory` | 背包, inv, i | 查看背包中的物品 |
| `/use` | 使用, eat | 使用背包中的物品 |
| `/tavern` | 酒馆 | 【管理员】开启/关闭酒馆模式 |
| `/listchar` | 角色列表 | 查看可用角色卡 |
| `/loadchar` | 加载角色 | 加载指定角色卡 |
| `/tavernchat` | 酒馆对话, tc | 与当前角色对话 |
| `/checkmonitor` | 监测状态 | 【调试】检查群聊监测状态 |
| `/help` | 帮助, ? | 显示帮助信息 |

### 查看他人数据（群聊限定）

白名单用户可以在群聊中查看其他成员的数码宝贝数据：

```bash
# 方式1：手动输入QQ号
/status 123456789
/path 123456789

# 方式2：@提及用户
/status @小明
/path @小红
```

**权限说明：**
- 仅限群聊中使用
- 仅限白名单用户使用
- 查询结果会显示对应群内的数码宝贝数据（自动拼接群ID）

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

## 战斗系统

### 攻击指令

使用 `/attack` 命令你的数码兽进行攻击：

```bash
/attack @小明          # 攻击指定用户的数码兽
/attack 123456789      # 通过QQ号攻击
/attack 大石头         # 攻击物体
/attack 路边的野狗     # 攻击任意描述的对象
```

### 战斗类型

**1. 数码兽对战**
- 攻击其他用户的数码兽
- AI生成战斗旁白描述整个过程
- 根据战斗结果双方情感属性会发生变化
- 被攻击方进入保护状态（默认5分钟）

**2. 攻击物体**
- 攻击任意描述的物体
- AI生成场景描述
- 仅攻击方情感属性变化

### 保护机制

- 被攻击的数码兽会进入 **5分钟保护期**
- 保护期内无法被再次攻击
- 可在配置中修改保护时间：`BattleProtectionSeconds`

### 注意事项

- 战斗消耗的 Token **不计入** 数码兽成长
- 不能攻击自己的数码兽
- 目标用户必须已有数码兽才能被攻击

## 经济系统

### 金币获得

与数码宝贝对话时会根据消耗的 **API Token** 获得金币：
- **计算公式**：金币 = Token消耗 ÷ 10
- 例如：一次对话消耗 150 Token，获得 15 金币

金币可以用于在商店购买物品。

### NPC 商店

使用 `/shop` 查看商店商品：

```bash
/shop              # 查看商店列表
/shop courage_cookie  # 购买勇气饼干
```

**内置食物列表**：

| 物品 | 效果 | 价格 |
|------|------|------|
| 勇气饼干 | 勇气+5 | 50 |
| 勇气牛排 | 勇气+15 | 150 |
| 友情糖果 | 友情+5 | 50 |
| 友情蛋糕 | 友情+15 | 150 |
| 爱心果实 | 爱心+5 | 50 |
| 爱心芭菲 | 爱心+15 | 150 |
| 知识面包 | 知识+5 | 50 |
| 智慧浓汤 | 知识+15 | 150 |
| 均衡便当 | 全属性+3 | 200 |
| 盛宴拼盘 | 全属性+10 | 500 |

### 物品使用

使用 `/use <物品ID>` 使用背包中的物品：

```bash
/use courage_cookie    # 使用勇气饼干
/inventory             # 查看背包
```

### 自定义物品

编辑 `Data/items_database.json` 添加新物品：

```json
{
  "id": "my_item",
  "name": "我的物品",
  "description": "物品描述",
  "price": 100,
  "type": "food",
  "effects": {
    "courage": 10,
    "friendship": 5
  }
}
```

**效果类型**：
- `courage` / `勇气` - 增加勇气值
- `friendship` / `友情` - 增加友情值
- `love` / `爱心` - 增加爱心值
- `knowledge` / `知识` - 增加知识值

## 识图系统

### 图片识别

使用 `/这是什么` 指令识别图片内容：

```bash
/这是什么       # 分析最近消息中的图片
/识图           # 同上
/img            # 同上
```

### 使用方法

1. **发送图片**到群里或私聊
2. **发送指令** `/这是什么`
3. Bot会分析图片并返回结果

**注意**：
- 会检查最近 **3条** 消息中的图片
- 如果找不到图片，会提示发送图片后再使用指令
- 需要配置识图模型才能使用

### 配置要求

需要在 `appsettings.json` 中配置支持视觉的AI模型：

**智谱GLM-4V配置示例：**
```json
{
  "AI": {
    "VisionModel": {
      "BaseUrl": "https://open.bigmodel.cn/api/paas/v4/chat/completions",
      "Model": "glm-4v"
    }
  }
}
```

**OpenAI GPT-4V配置示例：**
```json
{
  "AI": {
    "VisionModel": {
      "BaseUrl": "https://api.openai.com/v1/chat/completions",
      "Model": "gpt-4-vision-preview"
    }
  }
}
```

## 签到系统

### 每日签到

使用 `/checkin` 进行每日签到：

```bash
/checkin       # 签到获得奖励
/签到           # 同上
```

### 签到奖励

- **总签到天数**：累计签到天数统计
- **连续签到天数**：断签会重置为1
- **随机食物奖励**：根据连续签到天数获得不同品级的食物

### 奖励概率

| 连续签到天数 | 高品级食物概率 | 说明 |
|-------------|---------------|------|
| 1天 | 3% | 大概率获得普通食物 |
| 7天 | 23% | 较大概率获得优质食物 |
| 15天 | 50% | 一半概率获得高品级食物 |
| 30天 | 100% | **必定获得盛宴拼盘** |

**食物品级**：
- **普通**：勇气饼干、友情糖果等（+5情感值）
- **优质**：勇气牛排、友情蛋糕等（+15情感值）
- **顶级**：均衡便当（全属性+3）
- **至尊**：盛宴拼盘（全属性+10）- 连续30天专属

### 数码宝贝互动

签到后会触发数码宝贝的特殊对话，根据数码宝贝的性格和阶段生成独特的签到问候语。

### 注意事项

- 每天只能签到一次
- 连续签到断签后会重置为1天
- 签到奖励的物品会自动存入背包

## 酒馆系统（SillyTavern 兼容）

酒馆系统允许加载 SillyTavern 格式的角色卡，进行角色扮演对话。支持 PNG 和 JSON 格式的角色卡。

### 快速开始

```bash
# 1. 开启酒馆模式（管理员）
/酒馆 on

# 2. 查看可用角色
/listchar

# 3. 加载角色
/loadchar 小琪

# 4. 开始对话
@Bot /酒馆对话 你好呀！
```

### 角色卡格式

角色卡存放于 `Data/Characters/` 目录，支持：
- **PNG 格式**：SillyTavern 导出的角色卡图片（包含元数据）
- **JSON 格式**：纯文本角色定义文件

**JSON 角色卡示例**：
```json
{
  "name": "角色名称",
  "description": "角色描述",
  "personality": "性格特点",
  "scenario": "场景设定",
  "first_mes": "首次见面问候语",
  "mes_example": "对话示例",
  "tags": ["标签1", "标签2"]
}
```

### 自主发言功能

当群内讨论热烈时，角色会自动插入对话：

**触发条件**：
- 酒馆模式已开启
- 已加载角色
- 群内消息 ≥ 3 条
- 某个关键词出现 ≥ 2 次
- 不在冷却期（默认 5 分钟）

**示例**：
```
用户A: 今天测试一下
用户B: 测试这个功能
用户C: 再测试一次

→ 角色自动发言："（听到你们讨论得热烈，忍不住插话）关于测试，我有话要说..."
```

### 调试指令

使用 `/监测状态` 检查当前群是否满足触发条件：

```bash
/checkmonitor      # 查看群聊监测状态
/监测状态          # 同上
```

输出信息包括：
- 酒馆模式状态
- 角色加载状态
- 消息记录数量
- 关键词统计（Top 5）
- 各项触发条件检查

### 注意事项

- 角色卡需符合 SillyTavern V2 规范
- 自主发言触发后有 5 分钟冷却期
- 酒馆对话与数码宝贝系统相互独立
- 群内所有人共享同一个角色对话上下文

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

**Q: 数码宝贝数据会保存吗？**  
A: 是的！所有用户数据（数码宝贝状态、金币、背包等）都会持久化保存到 SQLite 数据库中，重启后数据仍然保留。只有对话历史会在重启后清空。

**Q: 如何备份数据？**  
A: 直接复制 `Data/bot_data.db` 文件即可备份所有用户数据。

**Q: 可以对接其他AI API吗？**  
A: 可以。DeepSeekClient实现了OpenAI兼容接口，可以替换为其他API（如智谱GLM）。

**Q: 群聊中如何触发Bot？**  
A: 需要在消息中@Bot，或发送以`/`开头的指令。

**Q: 群聊中每个人有自己的数码兽吗？**  
A: 取决于 `GroupDigimonMode` 配置。`Separate` 模式下每人有自己的数码兽（带昵称前缀），`Shared` 模式下全群共同培养一只。

**Q: NapCatQQ 和 Bot 必须运行在同一台机器上吗？**  
A: 不需要。只要网络可达，NapCatQQ 和 Bot 可以运行在不同的服务器上。只需配置正确的 WebSocket 和 HTTP 地址即可。

**Q: 如何查看/修改当前情感值？**  
A: 使用 `/status` 查看当前状态。管理员可使用 `/setemotion` 指令调整情感值（需在 `Admin.Whitelist` 中配置QQ号）。

## 技术栈

- **框架**: .NET 8, NapCatQQ (OneBot11协议)
- **AI**: DeepSeek API (OpenAI兼容)
- **数据**: SQLite (用户数据), JSON (配置)
- **编辑器**: WPF (.NET 8)

## 许可证

MIT License

---

🌟 如果觉得项目有用，请给个Star！
