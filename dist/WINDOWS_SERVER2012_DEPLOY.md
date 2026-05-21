# 轻燕工作台 - Windows Server 2012 部署指南

> 适用环境：Windows Server 2012 / 2012 R2  
> 版本：v1.0  
> 更新日期：2026-05-14

---

## 一、系统架构

```
客户端浏览器 ──▶ Nginx :80 (反向代理) ──▶ server.exe :8090 (后端API)
                                        │
                            ┌───────────┴───────────┐
                            ▼                       ▼
                    PostgreSQL :5432           Redis :6379
```

| 组件 | 角色 | 端口 |
|------|------|:----:|
| Nginx | 静态文件 + 反向代理 | 80 |
| server.exe | Go 后端 API 服务 | 8090 |
| PostgreSQL | 数据库 | 5432 |
| Redis | 缓存 / 限流 | 6379 |

---

## 二、前置条件

### 2.1 操作系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows Server 2012 / 2012 R2 |
| 体系架构 | x64（amd64） |
| 内存 | 最低 4 GB，建议 8 GB 以上 |
| 磁盘 | 最低 10 GB 可用空间 |
| 管理员权限 | 需要 Administrator 权限执行安装 |

### 2.2 离线安装包准备

在有互联网的机器上提前下载以下安装包，通过 U 盘或内网共享传入服务器：

| 软件 | 推荐版本 | 下载地址 |
|------|---------|---------|
| PostgreSQL | 12+ Windows x64 | https://www.enterprisedb.com/downloads/postgres-postgresql-downloads |
| Redis | 7.x Windows | https://github.com/tporadowski/redis/releases |
| Nginx | 1.24.x Windows | http://nginx.org/en/download.html |
| NSSM | 2.24 | https://nssm.cc/download |

> ⚠️ PostgreSQL 版本说明：
> - **PostgreSQL 12**：完全支持。启动时程序会自动创建 `pgcrypto` 扩展。如自动创建失败，需手动执行 `CREATE EXTENSION IF NOT EXISTS pgcrypto;`
> - **PostgreSQL 13+**：完全支持，`gen_random_uuid` 为内置函数无需额外配置
> - 下载地址使用 [EDB 官方安装包](https://www.enterprisedb.com/downloads/postgres-postgresql-downloads)

---

## 三、安装 PostgreSQL

### 3.1 安装步骤

1. 以 Administrator 身份运行 `postgresql-xx-windows-x64.exe`
2. 安装目录建议：`C:\PostgreSQL\15`
3. 数据目录建议：`C:\PostgreSQL\15\data`
4. 设置 `postgres` 超级用户的密码（请记住）
5. 端口保持默认 `5432`
6. Locale 选择 `Chinese (Simplified), China`

### 3.2 创建数据库

打开 **开始菜单 → PostgreSQL → pgAdmin** 或在命令行执行：

```cmd
:: 打开命令行
cd C:\PostgreSQL\15\bin
psql -U postgres

:: 在 psql 中执行
CREATE DATABASE labelpro;
\q
```

### 3.3 配置允许本地连接

编辑 `C:\PostgreSQL\15\data\pg_hba.conf`，确保有以下行：

```
# IPv4 local connections:
host    all             all             127.0.0.1/32            md5
```

重启 PostgreSQL 服务使配置生效：

```powershell
Restart-Service postgresql-x64-15
```

### 3.4 PostgreSQL 12 用户额外步骤

PostgreSQL 12 不内置 `gen_random_uuid()` 函数，需要通过 `pgcrypto` 扩展提供。

```cmd
cd C:\PostgreSQL\12\bin
psql -U postgres -d labelpro

:: 在 psql 中执行
CREATE EXTENSION IF NOT EXISTS pgcrypto;
\q
```

> ✅ **v1.0 已自动处理**：当前版本的 `server.exe` 启动时会自动执行 `CREATE EXTENSION IF NOT EXISTS pgcrypto`，PG12 用户无需手动操作。仅当自动执行失败时（如 postgres 用户无超级权限），才需手动执行上述命令。

---

## 四、安装 Redis

### 4.1 安装步骤

1. 将下载的 `Redis-x64-xxx.zip` 解压到 `C:\Redis`
2. 以管理员身份打开命令行：

```cmd
cd C:\Redis

:: 注册为 Windows 服务
redis-server.exe --service-install --service-name Redis redis.windows.conf

:: 启动服务
redis-server.exe --service-start --service-name Redis
```

### 4.2 设置 Redis 密码

编辑 `C:\Redis\redis.windows.conf`，找到并修改：

```
# 去掉注释并设置密码
requirepass 修改为实际密码
```

修改后重启服务：

```powershell
Restart-Service Redis
```

### 4.3 验证 Redis

```cmd
cd C:\Redis
redis-cli.exe -a 修改为实际密码 ping
:: 返回 PONG 即表示成功
```

---

## 五、安装 Nginx

### 5.1 安装步骤

1. 将下载的 `nginx-xxx.zip` 解压到 `C:\nginx`
2. Nginx 在 Windows 上没有服务注册功能，使用命令行启动，或通过 NSSM 注册服务

### 5.2 配置 Nginx

编辑 `C:\nginx\conf\nginx.conf`，替换为以下内容：

```nginx
worker_processes  auto;

events {
    worker_connections  1024;
}

http {
    include       mime.types;
    default_type  application/octet-stream;
    sendfile      on;
    keepalive_timeout  65;

    # Gzip 压缩
    gzip on;
    gzip_min_length 1k;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml;

    server {
        listen       80;
        server_name  你的服务器IP或域名;

        # 前端静态文件目录（部署时修改为实际路径）
        root   C:/轻燕工作台/web/;
        index  index.html;

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
            proxy_read_timeout 200s;   # AI 接口耗时较长，需要大超时
            proxy_connect_timeout 10s;
            proxy_send_timeout 10s;
        }

        # WebSocket 代理
        location /ws/ {
            proxy_pass http://127.0.0.1:8090;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_read_timeout 86400s;  # WebSocket 长连接
            proxy_connect_timeout 10s;
        }

        # 文件上传目录代理
        location /uploads/ {
            proxy_pass http://127.0.0.1:8090;
            proxy_read_timeout 60s;
        }

        # 错误页面
        error_page 500 502 503 504 /50x.html;
        location = /50x.html {
            root html;
        }
    }
}
```

### 5.3 测试 Nginx 配置

```cmd
cd C:\nginx
nginx -t
:: 显示 "syntax is ok" 和 "test is successful" 即配置正确
```

---

## 六、部署轻燕工作台

### 6.1 复制文件

将 `dist/` 目录整体复制到服务器的目标路径：

```cmd
:: 假设复制到 C:\轻燕工作台\
xcopy /E /Y dist\* C:\轻燕工作台\
```

最终目录结构：

```
C:\轻燕工作台\
├── server\
│   ├── server.exe          # 后端可执行文件
│   ├── config.json         # 配置文件
│   └── keys\
│       ├── private.pem     # JWT 私钥
│       └── public.pem      # JWT 公钥
├── web\
│   ├── index.html          # 前端入口
│   ├── favicon.svg
│   ├── logo.jpg
│   └── assets\
└── DEPLOY.md
```

### 6.2 修改配置文件

编辑 `C:\轻燕工作台\server\config.json`，按实际环境修改：

```json
{
  "server": {
    "port": 8090,
    "host": "0.0.0.0",
    "mode": "release",
    "_mode说明": "生产环境必须为 release",
    "read_timeout_seconds": 30,
    "write_timeout_seconds": 200,
    "max_request_body_mb": 10
  },
  "database": {
    "host": "127.0.0.1",
    "port": 5432,
    "user": "postgres",
    "password": "修改为第3步设置的密码",
    "dbname": "labelpro",
    "sslmode": "disable"
  },
  "redis": {
    "host": "127.0.0.1",
    "port": 6379,
    "password": "修改为第4步设置的Redis密码",
    "db": 0
  },
  "jwt": {
    "private_key_path": "./keys/private.pem",
    "public_key_path": "./keys/public.pem",
    "access_token_expire_seconds": 7200,
    "refresh_token_expire_seconds": 604800
  },
  "log": {
    "level": "warn",
    "_level说明": "生产环境建议 warn 或 error",
    "format": "json",
    "output_dir": "./logs",
    "enable_console": true
  },
  "storage": {
    "type": "local",
    "local_path": "./uploads"
  },
  "security": {
    "bcrypt_cost": 12,
    "cors_allowed_origins": [
      "http://你的服务器IP",
      "http://127.0.0.1:80"
    ]
  }
}
```

> ⚠️ `cors_allowed_origins` 必须填写实际的前端访问地址，内网示例：`["http://192.168.1.100"]`

### 6.3 创建运行时目录

```cmd
cd C:\轻燕工作台\server
mkdir uploads
mkdir logs
```

---

## 七、注册 Windows 服务

使用 NSSM（Non-Sucking Service Manager）将后端和 Nginx 注册为 Windows 服务，实现开机自启。

### 7.1 安装 NSSM

1. 将 `nssm-2.24.zip` 解压到 `C:\nssm`
2. 将 `C:\nssm\win64\nssm.exe` 复制到 `C:\Windows\System32\`（或添加到系统 PATH）

### 7.2 注册后端服务

以管理员身份打开命令行：

```cmd
nssm install 轻燕工作台后端
```

在弹出的 GUI 界面中填写：

| 字段 | 值 |
|------|-----|
| Path | `C:\轻燕工作台\server\server.exe` |
| Startup directory | `C:\轻燕工作台\server` |
| Arguments | 留空 |

点击 "Details" 标签，设置：

| 字段 | 值 |
|------|-----|
| Startup type | Automatic（开机自启） |

点击 "Install service" 完成。

### 7.3 注册 Nginx 服务

```cmd
nssm install 轻燕工作台Nginx
```

| 字段 | 值 |
|------|-----|
| Path | `C:\nginx\nginx.exe` |
| Startup directory | `C:\nginx` |
| Arguments | 留空 |

### 7.4 启动服务

```cmd
:: 启动后端
nssm start 轻燕工作台后端

:: 启动 Nginx
nssm start 轻燕工作台Nginx
```

### 7.5 验证服务

```cmd
nssm status 轻燕工作台后端
:: 应显示: SERVICE_RUNNING

nssm status 轻燕工作台Nginx
:: 应显示: SERVICE_RUNNING

:: 验证后端 API
curl http://127.0.0.1:8090/api/v1/ping
:: 应返回: {"code":200,"message":"success","data":{"ping":"pong"}}
```

### 7.6 NSSM 常用管理命令

```cmd
nssm start    服务名    # 启动服务
nssm stop     服务名    # 停止服务
nssm restart  服务名    # 重启服务
nssm status   服务名    # 查看服务状态
nssm remove   服务名    # 删除服务（需先停止）
nssm edit     服务名    # 重新打开配置界面
```

---

## 八、配置 Windows 防火墙

需要开放 80 端口供内网用户访问：

### 8.1 图形界面配置

1. 打开 **控制面板 → Windows 防火墙 → 高级设置**
2. 点击 **入站规则 → 新建规则**
3. 规则类型：**端口**
4. 协议和端口：**TCP，本地端口 80**
5. 操作：**允许连接**
6. 配置文件：全部勾选（域、专用、公用）
7. 名称：`轻燕工作台 - Web 访问`

### 8.2 命令行配置

以管理员身份运行 CMD：

```cmd
netsh advfirewall firewall add rule name="轻燕工作台-Web访问" dir=in action=allow protocol=TCP localport=80

:: 如果后端也需要外网直接访问（一般不需要）
netsh advfirewall firewall add rule name="轻燕工作台-后端API" dir=in action=allow protocol=TCP localport=8090
```

---

## 九、验证部署

### 9.1 检查服务运行状态

```cmd
nssm status 轻燕工作台后端
nssm status 轻燕工作台Nginx

:: 或使用 PowerShell
Get-Service -Name "轻燕工作台后端"
```

### 9.2 后端 API 测试

```cmd
:: 测试基础连通性
curl http://127.0.0.1:8090/api/v1/ping

:: 测试登录
curl -X POST http://127.0.0.1:8090/api/v1/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"password\":\"Admin@123\"}"
```

### 9.3 前端访问测试

在内网其他电脑浏览器中访问：

```
http://服务器IP地址/
```

应看到轻燕工作台登录页面，Logo 正常显示。

### 9.4 默认账号

| 用户名 | 密码 | 角色 |
|--------|------|------|
| `admin` | `Admin@123` | 超级管理员 |

> ⚠️ 首次登录后请立即修改默认密码。

---

## 十、配置 AI 智能分析（可选）

如需使用 AI 智能分析，需要在内网或可访问的网络中部署 AI 推理服务。

### 10.1 内网 AI 服务方案

| 方案 | 地址 | 说明 |
|------|------|------|
| Ollama | `http://内网IP:11434` | 免费开源，支持 DeepSeek/Qwen 等模型 |
| vLLM | `http://内网IP:8000` | 高性能推理引擎 |
| Open WebUI | 可提供 OpenAI 兼容 API | 基于 Ollama 的图形化界面 |

### 10.2 配置 AI

登录系统后，进入 **系统设置 → AI 配置**，填写内网 AI 服务地址。或者通过 API 创建：

```cmd
:: 先登录获取 Token
curl -X POST http://127.0.0.1:8090/api/v1/auth/login ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"password\":\"Admin@123\"}"

:: 创建 AI 配置（将 <TOKEN> 替换为登录返回的 access_token）
curl -X POST http://127.0.0.1:8090/api/v1/system/ai-configs ^
  -H "Authorization: Bearer <TOKEN>" ^
  -H "Content-Type: application/json" ^
  -d "{\"name\":\"内网AI\",\"api_endpoint\":\"http://192.168.1.200:11434\",\"api_key\":\"ollama\",\"model_name\":\"deepseek-r1:14b\",\"is_active\":true}"
```

---

## 十一、数据库备份

### 11.1 手动备份

```cmd
cd C:\PostgreSQL\15\bin
pg_dump -U postgres -F c -b -v -f C:\backup\labelpro_%date:~0,4%%date:~5,2%%date:~8,2%.dump labelpro
```

### 11.2 自动备份（计划任务）

创建 `C:\轻燕工作台\backup.bat`：

```batch
@echo off
set BACKUP_DIR=C:\backup
set PG_BIN=C:\PostgreSQL\15\bin
set PGPASSWORD=你的数据库密码

if not exist %BACKUP_DIR% mkdir %BACKUP_DIR%

set DT=%date:~0,4%%date:~5,2%%date:~8,2%

%PG_BIN%\pg_dump -U postgres -F c -f %BACKUP_DIR%\labelpro_%DT%.dump labelpro

:: 删除 7 天前的备份
forfiles /p %BACKUP_DIR% /m *.dump /d -7 /c "cmd /c del @path"
```

打开 **任务计划程序**，创建每日备份任务：

1. 触发器：每天凌晨 3:00
2. 操作：启动程序 `C:\轻燕工作台\backup.bat`
3. 勾选 **不管用户是否登录都要运行**

---

## 十二、常见问题

### Q1：启动 server.exe 提示缺少 DLL

```
无法启动此程序，因为计算机中丢失 VCRUNTIME140.dll
```

**解决**：安装 Visual C++ 2015-2022 Redistributable（x64）：  
https://aka.ms/vs/17/release/vc_redist.x64.exe

### Q2：Nginx 启动报错

```
bind() to 0.0.0.0:80 failed (10013: An attempt was made to access a socket...)
```

**原因**：80 端口被占用（通常是 IIS 或 SQL Server Reporting Services）

```cmd
:: 查看 80 端口占用
netstat -ano | findstr :80

:: 停止 IIS 服务（如果存在）
iisreset /stop

:: 或者修改 Nginx 监听端口为 8080，并相应调整防火墙规则
```

### Q3：数据库连接失败

```cmd
:: 检查 PostgreSQL 服务是否启动
sc query postgresql-x64-15

:: 测试连接
cd C:\PostgreSQL\15\bin
psql -U postgres -d labelpro -h 127.0.0.1
```

### Q4：AI 功能调用失败

1. 确认已在 **系统设置 → AI 配置** 中配置有效的 AI 服务
2. 检查 `config.json` 中 `server.write_timeout_seconds` 是否 >= 200
3. 确认 AI 服务在内网中可达：`curl http://AI服务IP:端口`

### Q5：前端页面空白

1. 检查 Nginx 是否运行：`nssm status 轻燕工作台Nginx`
2. 检查 `C:\nginx\conf\nginx.conf` 中 `root` 路径是否正确
3. 确认 `C:\轻燕工作台\web\index.html` 文件存在

### Q6：服务启动后立即停止

查看后端日志：

```cmd
type C:\轻燕工作台\server\logs\server.log
```

常见原因：数据库密码错误、Redis 密码错误、JWT 密钥缺失。

### Q7：PostgreSQL 12 报错 gen_random_uuid 不存在

```
main.go:55 函数gen_random_uuid不存在
failed to auto migrate database
```

**原因**：PostgreSQL 12 不内置 `gen_random_uuid()` 函数。

**解决**：

```cmd
cd C:\PostgreSQL\12\bin
psql -U postgres -d labelpro -c "CREATE EXTENSION IF NOT EXISTS pgcrypto;"
```

> ✅ v1.0 版本已自动处理：`server.exe` 启动时自动执行该扩展创建，新版本无需手动操作。

### Q8：内网部署后页面卡住 15 秒，控制台报 fonts.googleapis.com 请求失败

```
fonts.googleapis.com/css2?family=Inter:wght@... 请求超时
```

**原因**：前端 CSS 中引用了 Google Fonts CDN，内网无法访问导致浏览器等待超时。

**解决**：v1.0 最新版已移除此引用。如使用旧版前端包，替换 `web/` 目录即可。

> ✅ 已修复：当前版本已移除所有 `fonts.googleapis.com` 引用，使用系统自带中文字体（Microsoft YaHei / PingFang SC）。

### Q9：访问页面报 "Expected a JavaScript module but server responded with MIME type text/html"

```
Failed to load module script: Expected a JavaScript-or-Wasm module script but the
server responded with a MIME type of "text/html".
```

**原因**：`dist/web/` 下的文件目录结构与 `index.html` 引用不匹配。常见原因是复制前端文件时未保留 `assets/` 子目录（所有 JS 文件被平铺到了 `web/` 根目录）。

**解决**：

```cmd
:: 1. 清空旧文件
rd /s /q C:\轻燕工作台\web

:: 2. 正确复制（保留子目录结构）
xcopy /E /Y 新版本\dist\* C:\轻燕工作台\web\

:: 3. 确认 assets 目录存在
dir C:\轻燕工作台\web\assets

:: 4. 重启 Nginx
nssm stop 轻燕工作台Nginx
nssm start 轻燕工作台Nginx
```

> ⚠️ 使用 `xcopy /E` 可以保留子目录结构，`Copy-Item -Recurse` 同理。切勿将 JS 文件直接复制到 `web/` 根目录下。

---

## 十三、更新升级

```cmd
:: 1. 停止服务
nssm stop 轻燕工作台后端
nssm stop 轻燕工作台Nginx

:: 2. 备份旧版本
copy C:\轻燕工作台\server\server.exe C:\轻燕工作台\server\server.exe.bak

:: 3. 替换新文件
xcopy /E /Y 新版本\server\* C:\轻燕工作台\server\
xcopy /E /Y 新版本\web\* C:\轻燕工作台\web\

:: 4. 启动服务
nssm start 轻燕工作台后端
nssm start 轻燕工作台Nginx

:: 5. 验证
curl http://127.0.0.1:8090/api/v1/ping
```

---

## 十四、安全清单

- [ ] `config.json` 中 `server.mode` 已改为 `release`
- [ ] `security.cors_allowed_origins` 已配置为实际内网地址
- [ ] 数据库密码已修改为非默认值
- [ ] Redis 密码已设置
- [ ] 默认管理员（admin）密码已修改
- [ ] Windows 防火墙仅开放 80 端口，8090 仅对 127.0.0.1 监听
- [ ] 已配置数据库定期备份计划
- [ ] JWT 私钥文件 `keys/private.pem` 权限已限制（仅 Administrator 可读）
- [ ] Windows Server 已安装最新安全补丁

---

## 十五、服务管理速查

| 操作 | 命令 |
|------|------|
| 启动后端 | `nssm start 轻燕工作台后端` |
| 停止后端 | `nssm stop 轻燕工作台后端` |
| 重启后端 | `nssm restart 轻燕工作台后端` |
| 查看后端状态 | `nssm status 轻燕工作台后端` |
| 启动 Nginx | `nssm start 轻燕工作台Nginx` |
| 停止 Nginx | `nssm stop 轻燕工作台Nginx` |
| 重载 Nginx | `C:\nginx\nginx -s reload` |
| 查看后端日志 | `type C:\轻燕工作台\server\logs\server.log` |
| 查看 Nginx 日志 | `type C:\nginx\logs\error.log` |
| 手动备份数据库 | `C:\PostgreSQL\15\bin\pg_dump -U postgres -F c -f backup.dump labelpro` |

---

> 📧 技术支持：请联系开发团队  
> 📖 完整部署参考：`DEPLOY.md`（含 Linux 部署方式）
