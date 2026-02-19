# 部署前检查清单

## ✅ 代码准备

- [ ] 所有单元测试通过 (`dotnet test`)
- [ ] 集成测试通过 (`dotnet run --project tests/IntegrationTest`)
- [ ] 代码已提交到版本控制
- [ ] 配置文件已更新为生产环境设置

## ✅ 配置文件检查

### appsettings.json

```json
{
  "QQBot": {
    "NapCat": {
      "ConnectionType": "WebSocketReverse",
      "WebSocketHost": "127.0.0.1",
      "WebSocketPort": 5140,
      "AccessToken": "",
      "HttpApiUrl": "http://127.0.0.1:3000",
      "HttpAccessToken": "",
      "PostPath": "/onebot",
      "AutoReconnect": true,
      "ReconnectInterval": 10
    }
  },
  "AI": {
    "Provider": "deepseek",
    "ApiKey": "sk-xxxxxxxx",
    "Model": "deepseek-chat",
    "TimeoutSeconds": 60,
    "Temperature": 0.8,
    "MaxTokens": 1000
  },
  "Data": {
    "DigimonDatabasePath": "Data/digimon_database.json"
  }
}
```

**检查项：**
- [ ] NapCatQQ HTTP API 地址正确
- [ ] NapCatQQ WebSocket 地址正确
- [ ] API Key 已填写且有效
- [ ] 模型名称正确
- [ ] 数据库路径正确

### NapCatQQ 配置 (onebot11.json)

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
        "host": "0.0.0.0",
        "port": 3000
      }
    ]
  }
}
```

**检查项：**
- [ ] WebSocket 反向连接已启用
- [ ] URL 地址与 Bot 配置匹配
- [ ] HTTP API 已启用
- [ ] 端口配置正确

## ✅ 服务器准备

- [ ] 已购买/准备好 Linux 服务器
- [ ] 可以通过 SSH 连接到服务器
- [ ] 服务器可以访问外网（测试：`ping api.deepseek.com`）
- [ ] 已安装 .NET 8 Runtime
- [ ] 已安装 Docker（如使用 Docker 部署 NapCatQQ）

### 服务器配置检查

```bash
# 登录服务器后执行以下检查

# 1. 检查 .NET 版本
dotnet --version
# 应该显示 8.0.x

# 2. 检查内存
free -h
# 建议至少 1GB（NapCatQQ 需要运行 QQ）

# 3. 检查磁盘空间
df -h
# 建议至少 2GB 可用

# 4. 检查网络
ping -c 3 api.deepseek.com
ping -c 3 www.qq.com

# 5. 检查 Docker（如需要）
docker --version
docker ps
```

## ✅ NapCatQQ 部署检查

### Docker 方式

```bash
# 1. 拉取镜像
docker pull mlikiowa/napcat-docker:latest

# 2. 创建配置目录
mkdir -p /opt/napcat/config

# 3. 运行容器
docker run -d \
  --name napcat \
  --restart unless-stopped \
  -p 3000:3000 \
  -p 5140:5140 \
  -v /opt/napcat/config:/app/config \
  mlikiowa/napcat-docker:latest

# 4. 查看二维码并扫码登录
docker logs -f napcat

# 5. 验证登录
curl http://127.0.0.1:3000/get_login_info
```

**检查项：**
- [ ] NapCatQQ 容器正在运行
- [ ] 端口 3000 和 5140 已监听
- [ ] QQ 已成功登录
- [ ] HTTP API 响应正常

### 脚本安装方式

```bash
# 1. 运行安装脚本
curl -o napcat.sh https://nclatest.znin.net/NapNeko/NapCat-Installer/main/script/install.sh
sudo bash napcat.sh

# 2. 按照提示扫码登录

# 3. 验证服务状态
sudo systemctl status napcat
```

## ✅ 构建检查

```bash
# 1. 清理旧构建
dotnet clean

# 2. 发布项目
dotnet publish src/DigimonBot.Host -c Release -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o ./publish

# 3. 检查输出文件
ls -la ./publish/
```

**确认包含以下文件：**
- [ ] `DigimonBot.Host` (可执行文件)
- [ ] `appsettings.json`
- [ ] `Data/digimon_database.json`

## ✅ API Key 测试

在部署前，确认 API Key 有效：

### DeepSeek 测试
```bash
curl https://api.deepseek.com/v1/models \
  -H "Authorization: Bearer sk-your-api-key"
```

### GLM 测试
```bash
curl https://open.bigmodel.cn/api/paas/v4/models \
  -H "Authorization: Bearer your-api-key"
```

**预期结果：** HTTP 200，返回模型列表

## ✅ 首次部署流程

### 方式1：使用部署脚本

```bash
# 给脚本添加执行权限
chmod +x deploy.sh

# 运行部署脚本
./deploy.sh root@your-server-ip
```

### 方式2：手动部署

```bash
# 1. 发布
dotnet publish -c Release -r linux-x64 -o ./publish

# 2. 上传
scp -r ./publish/* root@your-server:/opt/digimon-bot/

# 3. 创建用户
ssh root@your-server "useradd -r -s /bin/false digimonbot"
ssh root@your-server "chown -R digimonbot:digimonbot /opt/digimon-bot"
```

## ✅ 首次运行检查

### 1. 启动 NapCatQQ

```bash
# Docker 方式
docker start napcat
# 检查状态
docker ps | grep napcat

# 验证 HTTP API
curl http://127.0.0.1:3000/get_login_info
```

### 2. 启动 Bot（前台测试）

```bash
# 进入目录
cd /opt/digimon-bot

# 设置权限
chown digimonbot:digimonbot . -R

# 前台运行测试
./DigimonBot.Host

# 预期输出：
# [INFO] Starting Digimon Bot with NapCatQQ...
# [INFO] Connected to NapCatQQ WebSocket successfully!
```

**检查项：**
- [ ] Bot 成功连接到 NapCatQQ
- [ ] WebSocket 连接建立
- [ ] 无错误日志

## ✅ Systemd 配置

```bash
# 1. 创建服务文件
sudo vim /etc/systemd/system/digimon-bot.service

# 2. 重载配置
sudo systemctl daemon-reload

# 3. 设置开机自启
sudo systemctl enable digimon-bot

# 4. 启动服务
sudo systemctl start digimon-bot

# 5. 检查状态
sudo systemctl status digimon-bot
```

## ✅ 功能验证

### NapCatQQ 测试

- [ ] NapCatQQ 在线且 QQ 登录正常
- [ ] HTTP API 响应正常
- [ ] WebSocket 连接正常

### 私聊测试

1. [ ] 添加 Bot 为好友
2. [ ] 发送消息：你好
3. [ ] 收到数码宝贝回复

### 群聊测试

1. [ ] 将 Bot 拉入群聊
2. [ ] @Bot 发送消息
3. [ ] 收到回复
4. [ ] 发送 `/status` 查看状态

### 进化测试

1. [ ] 多次对话后检查进化进度
2. [ ] 达到条件后确认触发进化

## ✅ 监控配置

```bash
# 查看实时日志
sudo journalctl -u digimon-bot -f

# 查看最近100行日志
sudo journalctl -u digimon-bot -n 100

# 查看 NapCatQQ 日志（Docker）
docker logs -f napcat

# 检查进程
ps aux | grep DigimonBot
```

## ⚠️ 常见问题预案

| 问题 | 解决方案 |
|------|---------|
| Bot 无法连接 NapCatQQ | 检查 NapCatQQ 是否运行、端口是否正确 |
| NapCatQQ 登录失败 | 删除配置重新扫码登录 |
| AI无响应 | 检查 API Key 是否过期/欠费 |
| 内存不足 | 升级服务器配置（建议 2GB+） |
| 进程崩溃 | 查看日志 `journalctl -u digimon-bot -n 50` |
| NapCatQQ 掉线 | 检查 QQ 账号是否在其他地方登录 |

## 📋 部署后确认

- [ ] NapCatQQ 正常运行且 QQ 在线
- [ ] Bot 可以正常连接到 NapCatQQ
- [ ] Bot 可以正常接收私聊消息
- [ ] Bot 可以在群聊中响应 @
- [ ] AI 回复正常
- [ ] 进化系统工作正常
- [ ] 日志中没有错误
- [ ] Systemd 服务状态为 active
- [ ] 已设置开机自启
- [ ] NapCatQQ 已设置开机自启

---

**确认所有检查项后，部署完成！** 🎉
