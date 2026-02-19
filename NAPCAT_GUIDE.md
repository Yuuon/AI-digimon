# NapCatQQ 使用指南

## 什么是 NapCatQQ？

[NapCatQQ](https://github.com/NapNeko/NapCatQQ) 是一个基于 NTQQ 的 OneBot11 协议实现，支持：

- 完整的 OneBot11 协议
- HTTP API 调用
- WebSocket 正向/反向连接
- 私聊和群聊消息
- 丰富的消息类型（文本、图片、@、回复等）

## 架构关系

```
用户QQ <---> QQ服务器 <---> NapCatQQ <---> OneBot11协议 <---> DigimonBot
```

## 部署方式

### 方式一：Docker 部署（推荐）

#### 1. 安装 Docker

```bash
# 一键安装 Docker
curl -fsSL https://get.docker.com | sh

# 启动 Docker
sudo systemctl start docker
sudo systemctl enable docker
```

#### 2. 运行 NapCatQQ

```bash
# 创建配置目录
mkdir -p /opt/napcat/config

# 运行容器
docker run -d \
  --name napcat \
  --restart unless-stopped \
  -p 3000:3000 \
  -p 5140:5140 \
  -v /opt/napcat/config:/app/config \
  mlikiowa/napcat-docker:latest

# 查看日志
docker logs -f napcat
```

#### 3. 配置 OneBot11

创建配置文件 `/opt/napcat/config/onebot11.json`：

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

重启生效：
```bash
docker restart napcat
```

---

### 方式二：Linux 一键脚本

```bash
# 下载并运行安装脚本
curl -o napcat.sh https://nclatest.znin.net/NapNeko/NapCat-Installer/main/script/install.sh
sudo bash napcat.sh

# 按照提示完成安装
```

安装完成后，配置文件位于 `/opt/napcat/config/onebot11.json`。

---

### 方式三：Windows 部署

1. 下载 NapCatQQ 发行版：https://github.com/NapNeko/NapCatQQ/releases
2. 解压并运行 `napcat.exe`
3. 按照提示完成 QQ 登录
4. 编辑 `config/onebot11.json` 配置文件

---

## 首次登录

### 扫码登录

NapCatQQ 启动后会显示二维码：

```bash
# Docker 方式查看二维码
docker logs napcat | grep -A 30 "二维码"

# 或使用二维码转图片工具（如果在本地）
```

1. 使用手机 QQ 扫描二维码
2. 确认登录
3. 登录信息会自动保存

### 验证登录状态

```bash
# HTTP API 测试
curl http://127.0.0.1:3000/get_login_info

# 预期返回：
# {"data":{"nickname":"你的昵称","user_id":123456789},...}
```

---

## 配置说明

### OneBot11 网络配置

NapCatQQ 支持多种连接方式：

#### WebSocket 反向连接（推荐）

NapCatQQ 作为客户端，主动连接到 Bot 服务端。

```json
{
  "network": {
    "websocket_reverse": [
      {
        "enable": true,
        "url": "ws://127.0.0.1:5140/onebot",
        "access_token": "your-token"
      }
    ]
  }
}
```

**配置项：**
| 配置项 | 说明 |
|--------|------|
| `url` | Bot 服务的 WebSocket 地址 |
| `access_token` | 访问令牌（可选） |

#### WebSocket 正向连接

NapCatQQ 作为服务端，等待 Bot 连接。

```json
{
  "network": {
    "websocket": [
      {
        "enable": true,
        "host": "0.0.0.0",
        "port": 3001
      }
    ]
  }
}
```

#### HTTP 模式

```json
{
  "network": {
    "http": [
      {
        "enable": true,
        "host": "0.0.0.0",
        "port": 3000,
        "access_token": "your-token"
      }
    ],
    "http_post": [
      {
        "enable": true,
        "url": "http://127.0.0.1:5140/post",
        "secret": "your-secret"
      }
    ]
  }
}
```

---

## 与 Bot 集成

### 配置 Bot 连接参数

编辑 `appsettings.json`：

```json
{
  "QQBot": {
    "NapCat": {
      "ConnectionType": "WebSocketReverse",
      "WebSocketHost": "127.0.0.1",
      "WebSocketPort": 5140,
      "HttpApiUrl": "http://127.0.0.1:3000"
    }
  }
}
```

### 配置对应关系

| NapCatQQ 配置 | Bot 配置 | 说明 |
|--------------|---------|------|
| `websocket_reverse.url` | `WebSocketHost` + `WebSocketPort` | 反向 WS 地址 |
| `http.port` | `HttpApiUrl` | HTTP API 端口 |
| `access_token` | `AccessToken` / `HttpAccessToken` | 访问令牌 |

---

## 常用管理命令

### Docker 方式

```bash
# 查看状态
docker ps | grep napcat

# 查看日志
docker logs napcat
docker logs -f napcat  # 实时日志

# 重启
docker restart napcat

# 停止
docker stop napcat

# 启动
docker start napcat

# 更新到最新版
docker pull mlikiowa/napcat-docker:latest
docker rm -f napcat
# 重新运行容器
```

### 系统服务方式

```bash
# 查看状态
sudo systemctl status napcat

# 查看日志
sudo journalctl -u napcat -f

# 重启
sudo systemctl restart napcat

# 停止
sudo systemctl stop napcat

# 启动
sudo systemctl start napcat
```

---

## 故障排查

### 问题1：无法显示二维码

**原因：** QQ 登录状态异常

**解决：**
```bash
# 删除登录缓存，重新扫码
docker rm -f napcat
rm -rf /opt/napcat/config/*
# 重新运行容器
```

### 问题2：Bot 无法连接

**排查：**
```bash
# 1. 检查 NapCatQQ 是否监听端口
netstat -tlnp | grep -E '3000|5140'

# 2. 测试 HTTP API
curl http://127.0.0.1:3000/get_version_info

# 3. 检查 WebSocket 连接
curl -i -N \
  -H "Connection: Upgrade" \
  -H "Upgrade: websocket" \
  http://127.0.0.1:5140/onebot
```

### 问题3：消息发送失败

**原因：**
- 账号被禁言
- 不在群组中
- 消息内容过长

**排查：**
```bash
# 查看 NapCatQQ 日志
docker logs napcat | grep -i error
```

### 问题4：频繁掉线

**原因：**
- 网络不稳定
- QQ 账号在其他地方登录
- 被风控

**解决：**
```bash
# 配置自动重启
docker update --restart always napcat

# 或设置 systemd 自动重启
```

---

## 安全建议

1. **设置访问令牌**
   ```json
   {
     "access_token": "your-strong-token"
   }
   ```

2. **限制监听地址**
   - 如果 NapCatQQ 和 Bot 在同一服务器，使用 `127.0.0.1`
   - 分离部署时使用防火墙限制访问

3. **定期更新**
   ```bash
   docker pull mlikiowa/napcat-docker:latest
   ```

4. **监控日志**
   ```bash
   # 查看异常登录
docker logs napcat | grep -i "login\|error"
   ```

---

## 相关链接

- [NapCatQQ GitHub](https://github.com/NapNeko/NapCatQQ)
- [NapCatQQ 官方文档](https://napneko.github.io/)
- [OneBot11 协议](https://github.com/botuniverse/onebot-11)
- [API 文档](https://napcat.apifox.cn)

---

如有问题，请查看日志：
- NapCatQQ 日志：`docker logs -f napcat`
- Bot 日志：`sudo journalctl -u digimon-bot -f`
