# 更新指南 - 情感值管理指令

## 更新内容

1. **新增 `/setemotion` 指令** - 手动调整情感值（勇气、友情、爱心、知识）
2. **白名单机制** - 管理指令仅限白名单用户使用

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
  }
}
```

### 3. 启动服务

```bash
cd /opt/digimon-bot
screen -S digimonbot
./DigimonBot.Host
```

## 使用说明

### 查看当前情感值
```
/setemotion show
```

### 增加情感值
```
/setemotion courage 10      # 勇气+10
/setemotion friendship 5    # 友情+5
/setemotion love -3         # 爱心-3
```

### 设置情感值（直接设定）
```
/setemotion courage=50      # 设置勇气为50
/setemotion love=30         # 设置爱心为30
```

### 重置所有情感值
```
/setemotion reset
```

### 支持的简写
- `c` / `勇气` → Courage
- `f` / `友情` → Friendship  
- `l` / `爱心` → Love
- `k` / `知识` → Knowledge

## 故障排查

**提示 "你没有权限使用此指令"**
- 检查 QQ 号是否正确添加到 `Admin.Whitelist`
- 确保配置文件已保存并重启服务
- 检查日志确认白名单加载成功

**情感值没有保存**
- 检查是否有写入权限
- 查看日志是否有错误信息
