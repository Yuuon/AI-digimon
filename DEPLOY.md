# 数码宝贝Bot 部署指南

## 目录

1. [服务器准备](#1-服务器准备)
2. [安装 NapCatQQ](#2-安装-napcatqq)
3. [项目发布](#3-项目发布)
4. [配置 NapCatQQ](#4-配置-napcatqq)
5. [部署运行](#5-部署运行)
6. [进程管理](#6-进程管理)
7. [常见问题](#7-常见问题)

---

## 1. 服务器准备

### 系统要求

- **OS**: Linux (Ubuntu 20.04+ / CentOS 8+ / Debian 11+) 或 Windows
- **内存**: 至少 1GB RAM (推荐 2GB，NapCatQQ 需要运行 QQ)
- **磁盘**: 至少 2GB 可用空间
- **网络**: 需要访问外网（调用AI API和QQ服务器）

### 安装 .NET 8 Runtime

```bash
# Ubuntu/Debian
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0

# CentOS/RHEL
sudo rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
sudo yum install -y aspnetcore-runtime-8.0

# 验证安装
dotnet --version
```

### 创建运行用户

```bash
sudo useradd -r -s /bin/false digimonbot
sudo mkdir -p /opt/digimon-bot
sudo chown digimonbot:digimonbot /opt/digimon-bot
```

---

## 2. 安装 NapCatQQ

NapCatQQ 是一个基于 NTQQ 的 OneBot11 协议实现，需要单独安装。

### 2.1 安装方式选择

| 方式 | 适用场景 | 难度 |
|------|---------|------|
| **Docker** | 推荐，最方便 | 简单 |
| **Linux 一键脚本** | Linux 服务器 | 简单 |
| **手动安装** | 需要自定义 | 中等 |

### 2.2 Docker 安装（推荐）

```bash
# 安装 Docker
curl -fsSL https://get.docker.com | sh

# 创建 NapCatQQ 配置目录
mkdir -p /opt/napcat/config

# 运行 NapCatQQ 容器
docker run -d \
  --name napcat \
  --restart unless-stopped \
  -p 3000:3000 \
  -p 5140:5140 \
  -v /opt/napcat/config:/app/config \
  -e NAPCAT_UID=1000 \
  -e NAPCAT_GID=1000 \
  mlikiowa/napcat-docker:latest

# 查看日志
docker logs -f napcat
```

### 2.3 Linux 一键脚本

```bash
# 使用官方安装脚本
curl -o napcat.sh https://nclatest.znin.net/NapNeko/NapCat-Installer/main/script/install.sh
sudo bash napcat.sh

# 按照提示完成安装和扫码登录
```

### 2.4 首次登录

NapCatQQ 启动后会显示二维码，使用手机 QQ 扫描登录：

```bash
# Docker 方式查看二维码
docker logs napcat | grep -A 20 "二维码"

# 或使用脚本方式
sudo napcat status
```

**注意：** 登录信息会保存在配置文件中，下次启动自动登录。

---

## 3. 项目发布

### 3.1 本地发布（推荐）

在本地开发机器上执行：

```bash
# 发布为 Linux x64 自包含应用
dotnet publish src/DigimonBot.Host -c Release -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o ./publish

# 查看发布文件
ls -la ./publish
```

### 3.2 上传到服务器

```bash
# 使用 scp 上传
scp -r ./publish/* root@your-server:/opt/digimon-bot/

# 或者使用 rsync
rsync -avz --progress ./publish/ root@your-server:/opt/digimon-bot/

# 设置权限
ssh root@your-server "chown -R digimonbot:digimonbot /opt/digimon-bot"
```

---

## 4. 配置 NapCatQQ

### 4.1 配置文件准备

编辑 NapCatQQ 的配置文件，启用 WebSocket 反向连接和 HTTP API：

**Docker 方式：**

```bash
# 编辑配置文件
cat > /opt/napcat/config/onebot11.json << 'EOF'
{
  "network": {
    "websocket_reverse": [
      {
        "enable": true,
        "url": "ws://127.0.0.1:5140/onebot",
        "message": {
          "report_self_message": false
        }
      }
    ],
    "http": [
      {
        "enable": true,
        "host": "0.0.0.0",
        "port": 3000,
        "message": {
          "post": []
        }
      }
    ]
  },
  "music_sign_url": "",
  "report_self_message": false,
  "enable_heartbeat": false
}
EOF

# 重启容器
docker restart napcat
```

**脚本安装方式：**

```bash
# 编辑配置文件
sudo nano /opt/napcat/config/onebot11.json
# 内容同上

# 重启服务
sudo systemctl restart napcat
```

### 4.2 Bot 配置文件

创建 `appsettings.json`：

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
    "ApiKey": "sk-xxxxxxxxxxxxxxxx",
    "Model": "deepseek-chat",
    "BaseUrl": null,
    "TimeoutSeconds": 60,
    "Temperature": 0.8,
    "MaxTokens": 1000
  },
  "Data": {
    "DigimonDatabasePath": "Data/digimon_database.json"
  }
}
```

**配置说明：**

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `WebSocketHost` | NapCatQQ WebSocket 监听地址 | 127.0.0.1 |
| `WebSocketPort` | NapCatQQ WebSocket 监听端口 | 5140 |
| `HttpApiUrl` | NapCatQQ HTTP API 地址 | http://127.0.0.1:3000 |
| `AccessToken` | WebSocket 访问令牌（可选） | 空 |
| `AutoReconnect` | 断线自动重连 | true |

---

## 5. 部署运行

### 5.1 文件结构确认

```
/opt/digimon-bot/
├── DigimonBot.Host          # 主程序
├── appsettings.json         # 配置文件
├── Data/
│   └── digimon_database.json # 数码宝贝数据库
```

### 5.2 启动顺序

**重要：** 必须先启动 NapCatQQ，确保 QQ 登录成功后，再启动 Bot。

```bash
# 1. 启动 NapCatQQ（如果未启动）
docker start napcat
# 或
sudo systemctl start napcat

# 2. 检查 NapCatQQ 状态
curl http://127.0.0.1:3000/get_login_info
# 应该返回登录的 QQ 信息

# 3. 启动 Bot
cd /opt/digimon-bot
./DigimonBot.Host
```

### 5.3 验证运行状态

```bash
# 检查 Bot 日志
journalctl -u digimon-bot -f

# 检查进程
ps aux | grep DigimonBot

# 检查 NapCatQQ 状态
docker ps | grep napcat
# 或
sudo systemctl status napcat
```

---

## 6. 进程管理

### 6.1 创建 Systemd 服务

创建 `/etc/systemd/system/digimon-bot.service`：

```ini
[Unit]
Description=Digimon QQ Bot
After=network.target napcat.service
Wants=napcat.service

[Service]
Type=simple
User=digimonbot
Group=digimonbot
WorkingDirectory=/opt/digimon-bot
ExecStart=/opt/digimon-bot/DigimonBot.Host
Restart=always
RestartSec=10

# 环境变量
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="AI__ApiKey=sk-xxxxxxxxxxxxxxxx"

# 日志输出
StandardOutput=journal
StandardError=journal

# 安全设置
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=/opt/digimon-bot

[Install]
WantedBy=multi-user.target
```

**注意：** 如果 NapCatQQ 使用 Docker，需要移除 `After=napcat.service` 和 `Wants=napcat.service`。

### 6.2 启动服务

```bash
# 重新加载 systemd
sudo systemctl daemon-reload

# 启动服务
sudo systemctl start digimon-bot

# 设置开机自启
sudo systemctl enable digimon-bot

# 查看状态
sudo systemctl status digimon-bot

# 查看日志
sudo journalctl -u digimon-bot -f
```

### 6.3 常用命令

```bash
# 启动
sudo systemctl start digimon-bot

# 停止
sudo systemctl stop digimon-bot

# 重启
sudo systemctl restart digimon-bot

# 查看日志（最近100行）
sudo journalctl -u digimon-bot -n 100

# 实时查看日志
sudo journalctl -u digimon-bot -f
```

---

## 7. 常见问题

### Q1: Bot 无法连接到 NapCatQQ

**排查步骤：**

```bash
# 1. 检查 NapCatQQ 是否运行
docker ps | grep napcat
# 或
sudo systemctl status napcat

# 2. 检查端口是否监听
netstat -tlnp | grep 5140
netstat -tlnp | grep 3000

# 3. 测试 HTTP API
curl http://127.0.0.1:3000/get_version_info

# 4. 检查配置文件
cat /opt/digimon-bot/appsettings.json | grep -A 10 NapCat
```

**解决方案：**
- 确保 NapCatQQ 已启动并登录
- 检查防火墙设置
- 确认配置中的端口与 NapCatQQ 一致

### Q2: NapCatQQ 登录失败

**原因：**
- 二维码过期
- 账号被风控
- 需要短信验证

**解决：**
```bash
# 1. 查看日志
docker logs -f napcat

# 2. 重启 NapCatQQ 重新扫码
docker restart napcat

# 3. 如果仍失败，删除配置重新登录
docker rm -f napcat
rm -rf /opt/napcat/config/*
# 重新运行容器并扫码
```

### Q3: 群聊中 Bot 不响应

**检查列表：**
1. NapCatQQ 是否正常在线？
2. Bot 是否成功连接到 NapCatQQ？
3. 是否@了Bot或发送了 `/` 开头的指令？
4. 查看 Bot 日志是否有消息接收记录

```bash
# 检查 NapCatQQ 是否收到消息
docker logs napcat | grep -i "群消息"

# 检查 Bot 日志
sudo journalctl -u digimon-bot -f
```

### Q4: AI 调用失败

**排查步骤：**
```bash
# 检查 API Key
curl -H "Authorization: Bearer sk-xxxxxxxx" \
  https://api.deepseek.com/v1/models

# 检查余额（DeepSeek）
# 登录 https://platform.deepseek.com/ 查看
```

### Q5: 如何更新 Bot

```bash
# 1. 停止服务
sudo systemctl stop digimon-bot

# 2. 备份配置
cp /opt/digimon-bot/appsettings.json /tmp/

# 3. 上传新版本
scp ./publish/* root@your-server:/opt/digimon-bot/

# 4. 恢复配置
cp /tmp/appsettings.json /opt/digimon-bot/
chown digimonbot:digimonbot /opt/digimon-bot/*.json

# 5. 启动服务
sudo systemctl start digimon-bot
```

### Q6: 如何更新 NapCatQQ

```bash
# Docker 方式
docker pull mlikiowa/napcat-docker:latest
docker rm -f napcat
# 重新运行容器（配置会保留）

# 脚本方式
sudo bash napcat.sh --update
```

### Q7: NapCatQQ 和 Bot 分离部署

如果需要在不同服务器上部署：

**NapCatQQ 服务器：**
```json
{
  "network": {
    "websocket_reverse": [
      {
        "enable": true,
        "url": "ws://bot-server-ip:5140/onebot"
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

**Bot 服务器：**
```json
{
  "QQBot": {
    "NapCat": {
      "WebSocketHost": "napcat-server-ip",
      "WebSocketPort": 5140,
      "HttpApiUrl": "http://napcat-server-ip:3000"
    }
  }
}
```

**注意：** 需要开放相应端口并配置防火墙。

---

## 附录

### 目录权限检查

```bash
# 确保权限正确
ls -la /opt/digimon-bot/

# 应该显示：
# -rw-r--r-- 1 digimonbot digimonbot appsettings.json
# drwxr-xr-x 2 digimonbot digimonbot Data
```

### 防火墙配置

```bash
# 如果 NapCatQQ 和 Bot 在同一服务器，无需开放端口

# 如果分离部署，在 NapCatQQ 服务器上：
sudo ufw allow from bot-server-ip to any port 3000
sudo ufw allow from bot-server-ip to any port 5140

# 或使用 iptables
sudo iptables -A INPUT -p tcp -s bot-server-ip --dport 3000 -j ACCEPT
sudo iptables -A INPUT -p tcp -s bot-server-ip --dport 5140 -j ACCEPT
```

### 日志轮转

创建 `/etc/logrotate.d/digimon-bot`：

```
/opt/digimon-bot/logs/*.log {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
    create 0644 digimonbot digimonbot
}
```

---

**部署完成！** 🎉

如有问题，请检查日志：
- Bot 日志：`sudo journalctl -u digimon-bot -f`
- NapCatQQ 日志：`docker logs -f napcat` 或 `sudo journalctl -u napcat -f`
