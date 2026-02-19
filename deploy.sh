#!/bin/bash
# 数码宝贝Bot 快速部署脚本
# 使用方法: ./deploy.sh [user@hostname]

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 配置
REMOTE_HOST="${1:-}"
REMOTE_DIR="/opt/digimon-bot"
LOCAL_PUBLISH_DIR="./publish"

echo -e "${GREEN}=== 数码宝贝Bot 部署脚本 ===${NC}"

# 检查参数
if [ -z "$REMOTE_HOST" ]; then
    echo -e "${RED}错误: 请提供远程服务器地址${NC}"
    echo "用法: $0 user@hostname"
    echo "示例: $0 root@192.168.1.100"
    exit 1
fi

# 步骤1: 本地发布
echo -e "\n${YELLOW}[1/5] 正在发布项目...${NC}"
if [ ! -d "$LOCAL_PUBLISH_DIR" ]; then
    echo "发布目录不存在，正在编译..."
    dotnet publish src/DigimonBot.Host -c Release -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -o "$LOCAL_PUBLISH_DIR"
else
    echo "使用现有发布目录: $LOCAL_PUBLISH_DIR"
fi

# 步骤2: 检查必要文件
echo -e "\n${YELLOW}[2/5] 检查必要文件...${NC}"
if [ ! -f "$LOCAL_PUBLISH_DIR/DigimonBot.Host" ]; then
    echo -e "${RED}错误: 未找到 DigimonBot.Host 可执行文件${NC}"
    exit 1
fi

if [ ! -f "$LOCAL_PUBLISH_DIR/Data/digimon_database.json" ]; then
    echo -e "${RED}错误: 未找到 digimon_database.json${NC}"
    exit 1
fi

# 步骤3: 上传到服务器
echo -e "\n${YELLOW}[3/5] 上传到服务器 $REMOTE_HOST...${NC}"

# 创建远程目录
ssh "$REMOTE_HOST" "mkdir -p $REMOTE_DIR/Data"

# 上传文件（排除配置文件）
rsync -avz --progress \
    --exclude='appsettings.json' \
    --exclude='device.json' \
    --exclude='keystore.json' \
    --exclude='qrcode.png' \
    "$LOCAL_PUBLISH_DIR/" "$REMOTE_HOST:$REMOTE_DIR/"

# 步骤4: 配置服务器
echo -e "\n${YELLOW}[4/5] 配置服务器...${NC}"

ssh "$REMOTE_HOST" << EOF
    # 创建用户（如果不存在）
    if ! id -u digimonbot &>/dev/null; then
        useradd -r -s /bin/false digimonbot
    fi
    
    # 设置权限
    chown -R digimonbot:digimonbot $REMOTE_DIR
    chmod +x $REMOTE_DIR/DigimonBot.Host
    
    # 安装 .NET Runtime（如果没有）
    if ! command -v dotnet &> /dev/null; then
        echo "安装 .NET 8 Runtime..."
        if command -v apt-get &> /dev/null; then
            # Ubuntu/Debian
            apt-get update
            apt-get install -y wget
            wget https://packages.microsoft.com/config/ubuntu/\$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
            dpkg -i packages-microsoft-prod.deb
            apt-get update
            apt-get install -y aspnetcore-runtime-8.0
        elif command -v yum &> /dev/null; then
            # CentOS/RHEL
            rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm
            yum install -y aspnetcore-runtime-8.0
        fi
    fi
EOF

# 步骤5: 创建 systemd 服务
echo -e "\n${YELLOW}[5/5] 配置 systemd 服务...${NC}"

ssh "$REMOTE_HOST" "cat > /etc/systemd/system/digimon-bot.service" << 'EOF'
[Unit]
Description=Digimon QQ Bot
After=network.target

[Service]
Type=simple
User=digimonbot
Group=digimonbot
WorkingDirectory=/opt/digimon-bot
ExecStart=/opt/digimon-bot/DigimonBot.Host
Restart=always
RestartSec=10
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

ssh "$REMOTE_HOST" "systemctl daemon-reload"

echo -e "\n${GREEN}=== 部署完成 ===${NC}"
echo ""
echo "接下来需要:"
echo "1. SSH 到服务器: ssh $REMOTE_HOST"
echo "2. 编辑配置:     vim $REMOTE_DIR/appsettings.json"
echo "3. 添加 API Key: 修改 AI__ApiKey"
echo "4. 设置 QQ 号:   修改 QQBot__Account"
echo ""
echo "首次运行（前台）:"
echo "  cd $REMOTE_DIR"
echo "  ./DigimonBot.Host"
echo ""
echo "扫码登录后，使用 systemd 管理:"
echo "  systemctl enable --now digimon-bot"
echo ""
echo "查看日志:"
echo "  journalctl -u digimon-bot -f"
