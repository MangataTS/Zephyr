# 轻燕工作台 - 部署指南

> 项目名称：轻燕工作台（Zephyr / LabelPro）  
> 版本：v1.0  
> 更新日期：2026-05-14

---

## 一、系统架构概览

```
┌─────────────┐     ┌──────────────┐     ┌───────────┐
│   浏览器    │────▶│  Nginx :80   │────▶│ Go Server │
│  (用户端)   │     │  (静态+反向代理)│     │  :8090    │
└─────────────┘     └──────────────┘     └───────────┘
                                              │  │
                                     ┌────────┘  └────────┐
                                     ▼                     ▼
                              ┌───────────┐       ┌──────────┐
                              │ PostgreSQL │       │  Redis   │
                              │   :5432   │       │  :6379   │
                              └───────────┘       └──────────┘
```

| 组件 | 说明 |
|------|------|
| **Go Server** | 后端 API 服务，监听 8090 端口 |
| **Web 静态文件** | Vue3 前端构建产物，由 Nginx 托管 |
| **Nginx** | 反向代理，API 转发到后端，静态文件直接返回 |
| **PostgreSQL** | 主数据库，存储所有业务数据 |
| **Redis** | 缓存 + 限流 + WebSocket 会话 |

---

## 二、内网部署可行性分析

### ✅ 可直接部署的组件

| 组件 | 内网可行性 | 说明 |
|------|:---:|------|
| **server.exe** | ✅ 完全可行 | Go 编译为独立二进制文件，无需 Go 运行时，无外部网络依赖 |
| **web/ 前端静态文件** | ✅ 完全可行 | 预构建的 HTML/JS/CSS，不依赖 npm/CDN，所有资源本地化 |
| **PostgreSQL** | ✅ 完全可行 | 使用离线安装包部署在内网服务器上 |
| **Redis** | ✅ 完全可行 | 使用离线安装包部署在内网服务器上 |
| **Nginx** | ✅ 完全可行 | 使用离线安装包，纯内网反向代理 |
| **JWT 密钥** | ✅ 完全可行 | 已打包在 `server/keys/` 中，纯本地运算 |
| **文件存储** | ✅ 完全可行 | 默认本地文件系统存储，无需外部对象存储 |
| **WebSocket** | ✅ 完全可行 | 仅接收客户端入站连接，不发起出站请求 |

### ⚠️ 需要调整的部分

| 问题 | 位置 | 解决方案 |
|------|------|---------|
| **AI 智能分析** | 调用 OpenAI 兼容 API | 内网部署私有 AI 服务（如 DeepSeek 内网版 / Ollama / vLLM），通过系统设置配置内网地址 |
| **CORS 跨域** | `config.json` → `security.cors_allowed_origins` | 改为内网实际访问地址（如 `http://192.168.1.100`） |
| **前端 Google Fonts** | 已修复：移除 `fonts.googleapis.com` 外部引用，改用系统自带字体 | ✅ 不再请求外网，避免内网 15s 超时卡顿 |
| **HTML 报告字体** | 已修复：移除 Google Fonts 外部引用，改用系统自带中文字体 | ✅ 本次打包已处理 |

### 🔧 内网环境离线部署依赖清单

以下为基础软件离线安装包，需提前下载并拷贝到内网服务器：

| 软件 | 推荐版本 | Windows 下载 | Linux 下载 |
|------|---------|-------------|-----------|
| PostgreSQL | 15+ | [postgresql.org/download/windows](https://www.postgresql.org/download/windows/) | `apt download postgresql-15` |
| Redis | 7+ | [github.com/tporadowski/redis/releases](https://github.com/tporadowski/redis/releases) | `apt download redis-server` |
| Nginx | 1.24+ | [nginx.org/en/download](http://nginx.org/en/download.html) | `apt download nginx` |

> 💡 对于完全离线环境，可在有网机器上用 `apt-get download` 下载 `.deb` 包及其依赖后传入内网安装。

---

## 三、环境要求

| 依赖 | 最低版本 | 说明 |
|------|---------|------|
| Windows Server 2016+ / Linux (Ubuntu 20.04+) | — | 操作系统 |
| PostgreSQL | 13+ | 数据库 |
| Redis | 6+ | 缓存 |
| Nginx | 1.20+ | 反向代理（可选，简易部署可跳过） |
| （无需安装 Go 运行时） | — | 后端已编译为独立 exe 文件 |

---

## 四、目录结构说明

```
dist/
├── server/                 # 后端服务
│   ├── server.exe          # 编译后的 Go 服务端（Windows 可执行文件）
│   ├── config.json         # 服务端配置文件
│   ├── keys/               # JWT RSA 密钥对
│   │   ├── private.pem     # 私钥（请妥善保管，不可泄漏）
│   │   └── public.pem      # 公钥
│   ├── uploads/            # 文件上传存储目录（空目录，运行时自动使用）
│   └── logs/               # 日志文件目录（空目录，运行时自动生成）
├── web/                    # 前端静态文件
│   ├── index.html          # 入口页面
│   ├── favicon.svg         # 网站图标
│   ├── logo.jpg            # 品牌 Logo
│   ├── icons.svg           # 图标精灵
│   └── assets/             # JS/CSS 静态资源
└── DEPLOY.md               # 本部署文档
```

---

## 五、部署步骤

### 步骤 1：环境准备

#### 1.1 安装 PostgreSQL

```bash
# Windows: 下载安装 https://www.postgresql.org/download/windows/
# Linux (Ubuntu):
sudo apt update
sudo apt install postgresql postgresql-contrib -y
sudo systemctl enable postgresql
sudo systemctl start postgresql
```

#### 1.2 创建数据库

```sql
-- 通过 psql 或 pgAdmin 执行
CREATE DATABASE labelpro;
```

#### 1.3 安装 Redis

```bash
# Windows: 下载安装 https://github.com/tporadowski/redis/releases
# Linux (Ubuntu):
sudo apt install redis-server -y
sudo systemctl enable redis-server
sudo systemctl start redis-server
```

#### 1.4 安装 Nginx（可选，简易部署可跳过）

```bash
# Windows: 下载安装 http://nginx.org/en/download.html
# Linux (Ubuntu):
sudo apt install nginx -y
sudo systemctl enable nginx
sudo systemctl start nginx
```

---

### 步骤 2：配置服务端

#### 2.1 上传文件

将 `dist/` 目录整体上传到服务器，推荐路径：

```
C:\轻燕工作台\        （Windows）
/opt/labelpro/       （Linux）
```

#### 2.2 修改配置文件

编辑 `server/config.json`，修改以下配置为实际环境值：

```json
{
  "database": {
    "host": "127.0.0.1",       // 数据库地址
    "port": 5432,
    "user": "postgres",        // 数据库用户名
    "password": "修改为实际密码", // 数据库密码
    "dbname": "labelpro"
  },
  "redis": {
    "host": "127.0.0.1",
    "port": 6379,
    "password": "修改为实际密码"  // Redis 密码
  },
  "server": {
    "port": 8090,
    "mode": "release",          // 生产环境改为 release
    "write_timeout_seconds": 200
  },
  "log": {
    "mode": "release",          // 生产环境改为 release
    "level": "warn"             // 生产环境建议 warn 或 error
  },
  "security": {
    "cors_allowed_origins": [
      "http://你的域名或IP"       // 替换为实际前端访问地址
    ]
  }
}
```

**关键配置项说明：**

| 配置项 | 说明 |
|--------|------|
| `server.mode` | 生产环境必须改为 `release` |
| `database.password` | 数据库密码 |
| `redis.password` | Redis 密码 |
| `security.cors_allowed_origins` | 前端访问地址列表 |
| `jwt.private_key_path` | JWT 私钥路径，默认为 `./keys/private.pem` |
| `write_timeout_seconds` | AI 功能需要较长时间，建议 200 |

#### 2.3 生成 JWT 密钥（如需要更换）

```bash
# 进入 server/keys/ 目录
cd server/keys/

# 生成 RSA 2048 位密钥对
openssl genrsa -out private.pem 2048
openssl rsa -in private.pem -pubout -out public.pem
```

> ⚠️ 当前已包含开发环境密钥对，生产环境请重新生成并妥善保管私钥。

---

### 步骤 3：启动后端服务

#### Windows

```powershell
cd C:\轻燕工作台\server\
.\server.exe
```

推荐使用 [NSSM](https://nssm.cc/) 注册为 Windows 服务：

```powershell
nssm install 轻燕工作台 C:\轻燕工作台\server\server.exe
nssm set 轻燕工作台 AppDirectory C:\轻燕工作台\server
nssm start 轻燕工作台
```

#### Linux

```bash
cd /opt/labelpro/server/
chmod +x server.exe
./server.exe
```

推荐使用 systemd 管理：

```bash
sudo tee /etc/systemd/system/labelpro.service << 'EOF'
[Unit]
Description=轻燕工作台服务
After=network.target postgresql.service redis-server.service

[Service]
Type=simple
WorkingDirectory=/opt/labelpro/server
ExecStart=/opt/labelpro/server/server.exe
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable labelpro
sudo systemctl start labelpro
```

#### 验证后端启动

```bash
curl http://localhost:8090/api/v1/ping
# 应返回: {"code":200,"message":"success","data":{"ping":"pong"}}
```

---

### 步骤 4：部署前端

#### 方式 A：Nginx 部署（推荐生产环境）

将 `web/` 目录内容复制到 Nginx 静态文件目录：

```bash
# Windows
xcopy /E /Y web\* C:\nginx\html\

# Linux
sudo cp -r web/* /var/www/labelpro/
```

Nginx 配置示例（`nginx.conf`）：

```nginx
server {
    listen 80;
    server_name 你的域名或IP;

    # 前端静态文件
    root /var/www/labelpro/;
    index index.html;

    # SPA 路由回退
    location / {
        try_files $uri $uri/ /index.html;
    }

    # API 反向代理到后端
    location /api/ {
        proxy_pass http://127.0.0.1:8090;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 200s;  # AI 接口需要较长超时
    }

    # WebSocket 代理
    location /ws/ {
        proxy_pass http://127.0.0.1:8090;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_read_timeout 86400s;
    }

    # 文件上传
    location /uploads/ {
        proxy_pass http://127.0.0.1:8090;
    }
}
```

重载 Nginx：

```bash
nginx -t              # 测试配置
nginx -s reload       # 重载
```

#### 方式 B：简易部署（无需 Nginx）

如果只是快速验证，可以将 `web/` 目录内容放到任意静态服务器上。

开发阶段可以直接用 `npx serve` 临时托管：

```bash
cd web/
npx serve -l 3000 -s .
```

> 注意：简易部署时前端会请求同源的 `/api/v1/...`，需要额外配置代理或修改 `VITE_API_BASE_URL`。

---

### 步骤 5：数据库初始化

后端启动时会**自动创建数据库表结构**（GORM AutoMigrate）。首次启动日志中会看到：

```
Database migration completed
```

如果系统包含种子数据脚本（预置用户、标签等），需单独执行：

```bash
cd server/
.\seed.exe       # Windows
./seed           # Linux（需另行编译）
```

#### 默认管理员账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `Admin@123` | 超级管理员 |

> ⚠️ 首次登录后请立即修改默认密码。

---

## 六、配置 AI 智能分析（可选）

系统支持 AI 智能分析功能，需要在系统设置中配置：

1. 登录系统后，进入「系统设置」→「AI 配置」
2. 填写 OpenAI 兼容的 API 参数：
   - API 端点：如 `https://api.deepseek.com` 或 `https://opencode.ai`
   - API 密钥：从服务商获取
   - 模型名称：如 `deepseek-chat`、`deepseek-v4-pro`

或者直接通过 API 创建：

```bash
curl -X POST http://localhost:8090/api/v1/system/ai-configs \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "AI配置",
    "api_endpoint": "https://api.deepseek.com",
    "api_key": "sk-xxx",
    "model_name": "deepseek-chat",
    "is_active": true
  }'
```

---

## 七、常见问题

### Q1：启动报错数据库连接失败

检查 `config.json` 中数据库配置是否正确，确认 PostgreSQL 服务已启动：

```bash
# Windows
net start postgresql
# Linux
sudo systemctl status postgresql
```

### Q2：AI 功能调用失败

1. 确认已在系统设置中配置 AI 服务
2. 检查 `config.json` 中 `server.write_timeout_seconds` 是否 >= 200（AI 调用可能耗时较长）

### Q3：前端页面空白或 404

1. 确认 Nginx 配置中 SPA 路由回退 `try_files $uri $uri/ /index.html` 已配置
2. 确认前端 `index.html` 中静态资源路径正确（以 `/assets/` 开头）

### Q4：WebSocket 连接失败

1. Nginx 需正确配置 `/ws/` 代理（含 Upgrade 头）
2. 如有防火墙，确认 WebSocket 端口未被拦截

### Q5：如何查看日志

```bash
# 服务端日志
cat server/logs/server.log
tail -f server/logs/server.log    # 实时查看

# 修改日志级别（config.json）
"log": { "level": "debug" }
```

### Q6：访问页面报 "Expected a JavaScript module but server responded with MIME type text/html"

**原因**：前端文件复制时未保留 `assets/` 子目录结构，nginx 找不到 JS 文件时触发 `try_files /index.html` 回退，返回 HTML 而非 JS。

**解决**：清空 web 目录后使用 `cp -r`（Linux）或 `xcopy /E`（Windows）重新复制，确保 `web/assets/` 目录存在。

```bash
# Linux
rm -rf /var/www/labelpro/*
cp -r dist/web/* /var/www/labelpro/
ls /var/www/labelpro/assets/   # 确认存在
nginx -s reload
```

```cmd
:: Windows
rd /s /q C:\轻燕工作台\web
xcopy /E /Y dist\web\* C:\轻燕工作台\web\
dir C:\轻燕工作台\web\assets
```

---

## 八、安全建议

1. ✅ `config.json` 中包含敏感信息，请设置文件权限为 600
2. ✅ `keys/private.pem` 是 JWT 签名私钥，妥善保管，不可外泄
3. ✅ 生产环境必须将 `server.mode` 改为 `release`
4. ✅ 修改默认管理员密码
5. ✅ 配置 HTTPS（建议使用 Let's Encrypt 免费证书）
6. ✅ 数据库密码使用强密码
7. ✅ 定期备份 PostgreSQL 数据库

---

## 九、端口清单

| 端口 | 服务 | 说明 |
|------|------|------|
| 80 | Nginx | 用户访问入口 |
| 8090 | Go Server | 后端 API（内部） |
| 5432 | PostgreSQL | 数据库（内部） |
| 6379 | Redis | 缓存（内部） |

---

## 十、更新升级

```bash
# 1. 停止服务
sudo systemctl stop labelpro

# 2. 备份旧版本
cp server/server.exe server/server.exe.bak

# 3. 替换新版本
cp /path/to/new/server.exe server/server.exe
cp -r /path/to/new/web/* /var/www/labelpro/

# 4. 重启服务
sudo systemctl start labelpro
sudo nginx -s reload
```

---

> 📧 技术支持：请联系开发团队  
> 📖 开发文档：参见项目根目录下的 `02-服务端开发文档.md` 和 `01-Web前端开发文档.md`
