# NapCatQQ Systemd 常驻化配置指南

## 概述

将 NapCatQQ 配置为 systemd 服务，实现：
- 开机自动启动
- 崩溃自动重启
- 日志统一管理
- 状态便捷查看

## 安装确认

首先确认 NapCatQQ 脚本安装方式的路径：

```bash
# 检查安装位置
which napcat
# 输出：/usr/local/bin/napcat

# 检查配置文件目录
ls -la /opt/napcat/
# 应该包含：config/ 目录
```

## 创建 Systemd 服务

### 1. 创建服务文件

```bash
sudo tee /etc/systemd/system/napcat.service > /dev/null << 'EOF'
[Unit]
Description=NapCatQQ Bot Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=root
Group=root

# 工作目录
WorkingDirectory=/opt/napcat

# 启动命令（使用脚本安装的命令）
ExecStart=/usr/local/bin/napcat start

# 停止命令
ExecStop=/usr/local/bin/napcat stop

# 重启策略
Restart=always
RestartSec=10

# 环境变量
Environment="NAPCAT_CONFIG_PATH=/opt/napcat/config"

# 日志输出
StandardOutput=journal
StandardError=journal

# 安全设置（可选）
# NoNewPrivileges=true
# ProtectSystem=strict
# ProtectHome=true

[Install]
WantedBy=multi-user.target
EOF
```

### 2. 重载 Systemd 配置

```bash
sudo systemctl daemon-reload
```

### 3. 启用开机自启

```bash
sudo systemctl enable napcat.service
```

## 服务管理命令

### 启动服务
```bash
sudo systemctl start napcat
```

### 停止服务
```bash
sudo systemctl stop napcat
```

### 重启服务
```bash
sudo systemctl restart napcat
```

### 查看状态
```bash
sudo systemctl status napcat
```

### 查看日志
```bash
# 实时查看日志
sudo journalctl -u napcat -f

# 查看最近 100 行
sudo journalctl -u napcat -n 100

# 查看今天的日志
sudo journalctl -u napcat --since today

# 查看完整日志
sudo journalctl -u napcat
```

## 高级配置

### 使用非 root 用户运行（推荐）

创建专用用户：

```bash
# 创建用户
sudo useradd -r -s /bin/false napcat

# 设置权限
sudo chown -R napcat:napcat /opt/napcat

# 更新服务文件
sudo tee /etc/systemd/system/napcat.service > /dev/null << 'EOF'
[Unit]
Description=NapCatQQ Bot Service
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=napcat
Group=napcat
WorkingDirectory=/opt/napcat

# 环境变量
Environment="HOME=/opt/napcat"
Environment="NAPCAT_CONFIG_PATH=/opt/napcat/config"

# 启动 NapCatQQ（需要找到实际的可执行文件）
ExecStart=/opt/napcat/napcat.sh

# 自动重启
Restart=always
RestartSec=10
StartLimitInterval=60s
StartLimitBurst=3

# 日志
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable napcat
```

### 启动前等待网络

如果 NapCatQQ 启动时网络尚未就绪，添加网络等待：

```ini
[Unit]
Description=NapCatQQ Bot Service
After=network-online.target systemd-resolved.service
Wants=network-online.target

[Service]
Type=simple
ExecStartPre=/bin/sleep 5  # 延迟 5 秒启动
ExecStart=/usr/local/bin/napcat start
Restart=always
RestartSec=10
```

### 与 DigimonBot 联动

如果 DigimonBot 和 NapCatQQ 在同一服务器，配置服务依赖：

**napcat.service：**
```ini
[Unit]
Description=NapCatQQ Bot Service
After=network-online.target

[Service]
Type=simple
ExecStart=/usr/local/bin/napcat start
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

**digimon-bot.service：**
```ini
[Unit]
Description=Digimon QQ Bot
After=network-online.target napcat.service
Wants=napcat.service

[Service]
Type=simple
User=digimonbot
WorkingDirectory=/opt/digimon-bot
ExecStart=/opt/digimon-bot/DigimonBot.Host
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

这样 DigimonBot 会在 NapCatQQ 启动后才启动。

## 故障排查

### 服务无法启动

```bash
# 查看详细错误
sudo systemctl status napcat -l

# 查看启动日志
sudo journalctl -u napcat --no-pager -n 50
```

**常见原因：**
1. QQ 未登录 - 需要先手动登录一次
2. 权限问题 - 检查 /opt/napcat 目录权限
3. 端口冲突 - 检查 3000/5140 端口是否被占用

### 设置自动登录

如果 NapCatQQ 重启后需要重新扫码，配置自动登录：

```bash
# 编辑配置文件
sudo nano /opt/napcat/config/onebot11.json

# 确保包含自动登录配置
{
  "auto_login": true,
  "account": {
    "uin": 你的QQ号
  }
}
```

### 服务频繁重启

```bash
# 查看重启原因
sudo journalctl -u napcat --since "1 hour ago" | grep -i "fail\|error\|restart"

# 限制重启次数
sudo systemctl edit napcat.service
```

添加：
```ini
[Service]
StartLimitInterval=60
StartLimitBurst=3
```

### QQ 掉线处理

配置心跳检测和自动重连：

```ini
[Service]
Restart=always
RestartSec=30
TimeoutStartSec=60
TimeoutStopSec=30
```

## 日志切割

防止日志文件过大：

```bash
sudo tee /etc/logrotate.d/napcat > /dev/null << 'EOF'
/var/log/napcat/*.log {
    daily
    rotate 7
    compress
    delaycompress
    missingok
    notifempty
    create 0644 root root
    sharedscripts
    postrotate
        systemctl reload napcat || true
    endscript
}
EOF
```

## 快速命令参考

```bash
# ===== 状态查看 =====
sudo systemctl status napcat           # 服务状态
sudo journalctl -u napcat -f           # 实时日志
sudo netstat -tlnp | grep napcat       # 端口监听

# ===== 服务控制 =====
sudo systemctl start napcat            # 启动
sudo systemctl stop napcat             # 停止
sudo systemctl restart napcat          # 重启
sudo systemctl reload napcat           # 重载配置

# ===== 开机设置 =====
sudo systemctl enable napcat           # 开机自启
sudo systemctl disable napcat          # 取消自启

# ===== 故障排查 =====
sudo journalctl -u napcat -n 100       # 最近100行日志
sudo journalctl -u napcat --since "1 hour ago"  # 最近1小时
sudo systemctl reset-failed napcat     # 重置失败状态
```

## 注意事项

1. **首次配置**：建议先手动运行 `napcat start` 确保 QQ 能正常登录，再配置服务

2. **登录状态**：扫码登录后，会话会保存在 /opt/napcat/config/，重启后自动登录

3. **权限问题**：如果使用非 root 用户，确保该用户有权限访问 QQ 相关文件

4. **防火墙**：如果分离部署，记得开放相应端口

5. **资源限制**：可在 [Service] 中添加资源限制
   ```ini
   MemoryLimit=512M
   CPUQuota=50%
   ```

---

配置完成后，NapCatQQ 将作为系统服务常驻运行，系统重启后会自动启动。
