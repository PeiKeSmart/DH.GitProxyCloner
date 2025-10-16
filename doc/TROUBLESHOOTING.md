# 故障排除指南

## 常见问题

### 问题 1：克隆时要求输入用户名和密码

**症状：**
```text
Username for 'http://proxy-server': 
Password for 'http://proxy-server':
```

**原因：** Git 凭证管理器（GCM）尝试获取凭证

**解决方案：**

```bash
# 方法 1：使用环境变量（推荐）
# Windows PowerShell
$env:GIT_TERMINAL_PROMPT = "0"
git clone http://proxy-server/user/repo

# Linux/Mac
export GIT_TERMINAL_PROMPT=0
git clone http://proxy-server/user/repo

# 方法 2：临时禁用凭证助手
git clone -c credential.helper= http://proxy-server/user/repo

# 方法 3：为该域名禁用凭证
git config --global credential.http://proxy-server.helper ""
```

---

### 问题 2：带 .git 后缀和不带后缀表现不同

**症状：**
```bash
git clone http://proxy/user/repo.git    # ❌ 要求密码
git clone http://proxy/user/repo        # ✅ 正常工作
```

**原因：** 早期版本路由不匹配 `.git` 后缀

**解决方案：** 

升级到 v1.1.0 或更高版本，已修复此问题。两种格式现在都完全支持。

---

### 问题 3：Authentication failed 错误

**症状：**
```text
remote: Invalid username or token.
fatal: Authentication failed
```

**原因：** 无效的 Authorization 头被转发

**解决方案：** 

升级到 v1.1.0 或更高版本，代理服务器会自动移除 Authorization 头。

---

### 问题 4：连接超时

**症状：**
```text
fatal: unable to access: Couldn't connect to server
```

**检查：**
1. 代理服务器是否在运行
2. 端口是否正确
3. 防火墙是否阻止

**解决方案：**
```powershell
# 测试端口
Test-NetConnection localhost -Port 17856

# 检查服务状态
Get-Process | Where-Object {$_.ProcessName -like "*GitProxy*"}
```

---

### 问题 5：SSL/TLS 证书警告

**症状：**
```text
warning: HTTPS connections may not be secure
```

**解决方案：**

```bash
# 使用 HTTP 端口（推荐用于开发）
git clone http://proxy-server/user/repo

# 或忽略 SSL 验证（不推荐用于生产）
git -c http.sslVerify=false clone https://proxy-server/user/repo
```

---

## 快速测试

### 验证代理是否正常工作

```bash
# 1. 测试连接
curl http://localhost:17856

# 2. 测试 Git 协议端点
curl "http://localhost:17856/octocat/Hello-World/info/refs?service=git-upload-pack"

# 3. 克隆测试仓库
git clone http://localhost:17856/octocat/Hello-World
```

---

## 清除 Git 凭证缓存

### Windows
```powershell
# 清除凭证管理器
cmdkey /list
cmdkey /delete:LegacyGeneric:target=git:http://proxy-server
```

### Linux
```bash
git credential-cache exit
```

### macOS
```bash
git credential-osxkeychain erase
# 然后输入: protocol=http, host=proxy-server
```

---

## 查看日志

代理服务器运行时会在终端输出日志：

```text
info: DH.GitProxyCloner.Controllers.GitSmartHttpController[0]
      Proxying Git request to: https://github.com/user/repo.git/info/refs
```

关键日志标识：
- ✅ `GitSmartHttpController` - 请求被正确路由
- ⚠️ `ForwardProxyController` - 可能路由不匹配
- ❌ `Authentication failed` - 凭证问题

---

## 获取帮助

如果以上方法都无法解决问题：

1. 查看 [CHANGELOG.md](../CHANGELOG.md) 确认版本功能
2. 检查 [README.md](../README.md) 了解基本用法
3. 提交 Issue 并附上：
   - 错误信息
   - Git 版本 (`git --version`)
   - 代理服务器日志
   - 完整的克隆命令
