# 更新指南

## v2.0 更新内容

### 1. 新增 `/setemotion` 指令 - 手动调整情感值
- 支持增加/减少/设置/重置情感值
- 仅限白名单用户使用

### 2. 查看他人数码宝贝数据（群聊限定）
- `/status [QQ号/@用户]` - 查看他人数码宝贝状态
- `/path [QQ号/@用户]` - 查看他人进化路线
- 仅限白名单用户在群聊中使用
- 自动拼接群ID，显示当前群的数码宝贝数据

---

## 部署步骤

### 1. 上传新版本

```bash
# 停止现有服务
screen -r digimonbot
# Ctrl+C 停止程序

# 备份原配置
cp /opt/digimon-bot/appsettings.json /tmp/appsettings-backup.json

# 上传新文件
scp ./publish/DigimonBot.Host root@your-server:/opt/digimon-bot/
```

### 2. 更新配置文件

编辑 `/opt/digimon-bot/appsettings.json`，添加白名单配置：

```json
{
  "QQBot": { ... },
  "AI": { ... },
  "Data": { ... },
  "Admin": {
    "Whitelist": [
      "你的QQ号",
      "其他管理员QQ号"
    ]
  },
  "GroupMode": {
    "GroupDigimonMode": "Separate"
  }
}
```

### 3. 启动服务

```bash
cd /opt/digimon-bot
screen -S digimonbot
./DigimonBot.Host
```

---

## 使用说明

### 查看他人数码宝贝数据（白名单限定）

**在群聊中使用：**

```bash
# 查看他人状态（手动输入QQ号）
/status 123456789

# 查看他人进化路线（@提及）
/path @小明
```

### 情感值管理（白名单限定）

```bash
# 查看当前情感值
/setemotion show

# 增加情感值
/setemotion courage 10      # 勇气+10
/setemotion love -5         # 爱心-5

# 设置情感值
/setemotion courage=50      # 设置勇气为50

# 重置所有情感值
/setemotion reset
```

---

## 故障排查

**提示 "你没有权限使用此指令"**
- 检查 QQ 号是否正确添加到 `Admin.Whitelist`
- 确保配置文件已保存并重启服务
- 查看日志确认白名单加载成功

**提示 "查看他人数据功能仅限群聊中使用"**
- 该功能只能在群聊中使用，私聊无法查看他人数据

**查看他人数据显示错误**
- 确认被查询用户在当前群内有数码宝贝数据
- 检查群聊隔离模式配置是否正确
