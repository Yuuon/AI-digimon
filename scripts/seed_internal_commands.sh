#!/bin/bash
# seed_internal_commands.sh
# One-shot script to insert all internal (hardcoded) bot commands as placeholder
# rows in the CustomCommands table so the AI agent can discover every command
# via a single  SELECT * FROM CustomCommands;  query.
#
# Placeholder rows use:
#   BinaryPath     = 'INTERNAL'   (marks them as built-in, not executable binaries)
#   OwnerUserId    = 'SYSTEM'
#   RequiresWhitelist = 0
#
# Safe to re-run: uses INSERT OR IGNORE so existing rows are not duplicated.
#
# Usage:
#   cd <project-root>
#   bash scripts/seed_internal_commands.sh

set -euo pipefail

DB="Data/kimi_data.db"

if [ ! -f "$DB" ]; then
    echo "ERROR: Database file '$DB' not found. Run from the project root directory." >&2
    exit 1
fi

echo "Seeding internal commands into $DB ..."

sqlite3 "$DB" <<'SQL'
BEGIN TRANSACTION;

-- status
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('status', '["状态", "s"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看数码兽状态', datetime('now'), 0);

-- path (evolution-path)
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('path', '["进化路线", "evo", "p"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看进化路线', datetime('now'), 0);

-- reset
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('reset', '["重置", "r"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 重置数码兽', datetime('now'), 0);

-- help
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('help', '["帮助", "h", "?"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看帮助信息', datetime('now'), 0);

-- jrrp
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('jrrp', '["今日人品", "人品", "运势"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 今日人品', datetime('now'), 0);

-- setemotion
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('setemotion', '["设置情感", "emotion"]', 'INTERNAL', 'SYSTEM', 1, '[内置] 设置情感值（需要白名单）', datetime('now'), 0);

-- shop
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('shop', '["商店", "buy", "购买"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 商店 / 购买物品', datetime('now'), 0);

-- inventory
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('inventory', '["背包", "inv", "bag", "i"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看背包', datetime('now'), 0);

-- use (item)
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('use', '["使用", "eat", "吃"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 使用物品', datetime('now'), 0);

-- attack
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('attack', '["攻击", "a", "fight"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 发起攻击', datetime('now'), 0);

-- checkin
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('checkin', '["签到", "sign", "打卡"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 每日签到', datetime('now'), 0);

-- whatisthis
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('whatisthis', '["这是什么", "识图", "img"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 识图（图片识别）', datetime('now'), 0);

-- tavern
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('tavern', '["酒馆"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 酒馆模式开关', datetime('now'), 0);

-- listchar
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('listchar', '["角色列表", "charlist"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 列出酒馆角色', datetime('now'), 0);

-- loadchar
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('loadchar', '["加载角色", "load"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 加载酒馆角色', datetime('now'), 0);

-- tavernchat
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('tavernchat', '["酒馆对话", "tc"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 酒馆角色对话', datetime('now'), 0);

-- checkmonitor
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('checkmonitor', '["监测状态", "debugmonitor"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 检查群聊监测状态', datetime('now'), 0);

-- reloadtavern
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('reloadtavern', '["重载酒馆配置", "reloadt"]', 'INTERNAL', 'SYSTEM', 1, '[内置] 重新加载酒馆配置（需要白名单）', datetime('now'), 0);

-- specialfocus
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('specialfocus', '["特别关注", "sf"]', 'INTERNAL', 'SYSTEM', 1, '[内置] 特别关注管理（需要白名单）', datetime('now'), 0);

-- reloadpersonality
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('reloadpersonality', '["重载性格配置", "reloadp"]', 'INTERNAL', 'SYSTEM', 1, '[内置] 重新加载性格配置（需要白名单）', datetime('now'), 0);

-- reloaddialogue
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('reloaddialogue', '["重载对话配置", "reloadd"]', 'INTERNAL', 'SYSTEM', 1, '[内置] 重新加载对话配置（需要白名单）', datetime('now'), 0);

-- evolist
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('evolist', '["进化列表", "evo", "el"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看可进化列表', datetime('now'), 0);

-- evoselect
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('evoselect', '["进化选择", "evos", "es"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 选择进化', datetime('now'), 0);

-- kimi
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('kimi', '["kimichat", "kimi助手"]', 'INTERNAL', 'SYSTEM', 0, '[内置] Kimi AI 代码助手', datetime('now'), 0);

-- customcmds
INSERT OR IGNORE INTO CustomCommands (Name, Aliases, BinaryPath, OwnerUserId, RequiresWhitelist, Description, CreatedAt, UseCount)
VALUES ('customcmds', '["customs", "cmds"]', 'INTERNAL', 'SYSTEM', 0, '[内置] 查看自定义命令列表', datetime('now'), 0);

COMMIT;
SQL

echo "Done. Verifying:"
sqlite3 "$DB" -header -column "SELECT Name, Aliases, Description FROM CustomCommands WHERE BinaryPath = 'INTERNAL' ORDER BY Name;"
echo ""
echo "Total internal placeholders: $(sqlite3 "$DB" "SELECT COUNT(*) FROM CustomCommands WHERE BinaryPath = 'INTERNAL';")"
