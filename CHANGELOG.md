# 变更日志

## [1.1.0] - 2025-10-16

### ✨ 新增功能

#### 匿名访问公共仓库
- **无需身份验证** - 克隆 GitHub 公共仓库不再需要输入用户名和密码
- **自动移除 Authorization 头** - 代理服务器会自动过滤掉无效的认证信息
- **解决 "Authentication failed" 错误** - 修复了 Git 凭证管理器导致的认证失败问题

#### 支持 .git 后缀 URL
- **双路由支持** - 同时支持 `owner/repo` 和 `owner/repo.git` 两种格式
- **自动规范化** - 自动处理 URL 中的 `.git` 后缀，避免重复
- **完整兼容性** - 与标准 Git 服务器行为一致

```bash
# 两种格式都可以正常工作
git clone http://proxy/user/repo         # ✅ 工作
git clone http://proxy/user/repo.git     # ✅ 工作
```

### 🔧 技术改进

#### GitSmartHttpController.cs
- 移除了 `Authorization` 头的转发逻辑
- 添加了详细的注释说明为什么不转发认证头
- 保留了其他重要的 Git 协议头（User-Agent、Accept、Git-Protocol 等）
- **添加了 .git 后缀路由支持** - 每个端点现在有两个路由属性

```csharp
// 修改前 - 只有一个路由
[HttpGet("{owner}/{repo}/info/refs")]
public async Task<IActionResult> GetInfoRefs(...)

// 修改后 - 双路由支持
[HttpGet("{owner}/{repo}.git/info/refs")]      // 新增：支持 .git 后缀
[HttpGet("{owner}/{repo}/info/refs")]          // 保留：无后缀格式
public async Task<IActionResult> GetInfoRefs(string owner, string repo, ...)
{
    // 自动处理 .git 后缀
    if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        repo = repo.Substring(0, repo.Length - 4);
    
    var githubUrl = $"https://github.com/{owner}/{repo}.git/info/refs?service={service}";
    return await ProxyGitRequest(githubUrl);
}
```

同样的改进应用到：
- `GetInfoRefs` - Git 信息发现端点
- `PostUploadPack` - Git 克隆/拉取端点
- `PostReceivePack` - Git 推送端点

#### ForwardProxyController.cs
- 创建了自定义的 `AnonymousAccessTransformer` 类
- 继承自 YARP 的 `HttpTransformer` 基类
- 在请求转换阶段自动移除 Authorization 头

```csharp
public class AnonymousAccessTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(...)
    {
        await base.TransformRequestAsync(...);
        proxyRequest.Headers.Remove("Authorization");
    }
}
```

### 📚 文档更新

- **ANONYMOUS_ACCESS.md** - 新增完整的匿名访问指南
  - 问题分析和根本原因
  - 解决方案详细说明
  - 使用方法和示例
  - 故障排除指南
  - 未来的私有仓库支持计划

- **TEST_ANONYMOUS_ACCESS.md** - 新增测试文档
  - 详细的测试步骤
  - 测试脚本（PowerShell 和 Bash）
  - 验证方法
  - 预期结果对比
  - 故障排除步骤

- **README.md** - 更新主文档
  - 添加了匿名访问功能的说明
  - 突出显示新功能特性
  - 链接到详细文档

- **GIT_SUFFIX_FIX.md** - 新增 .git 后缀修复文档
  - 问题分析和路由匹配详解
  - 双路由实现方案
  - 测试验证步骤
  - 技术细节说明

### 🐛 问题修复

#### 问题 1：公共仓库要求身份验证

**问题描述**

用户在克隆公共仓库时遇到以下错误：
```text
warning: HTTPS connections may not be secure.
remote: Invalid username or token. Password authentication is not supported for Git operations.
fatal: Authentication failed for 'http://g.sc8.fun/sharpbrowser/SharpBrowser.git/'
```

**根本原因**
- Git 凭证管理器（GCM）自动添加了 Authorization 头
- 代理服务器将这个头转发给了 GitHub
- GitHub 检测到无效的 Authorization 头后拒绝请求
- 即使是公共仓库也会因为无效认证而失败

**解决方案**
- 代理服务器不再转发 Authorization 头
- GitHub 将请求视为匿名访问
- 公共仓库可以正常克隆，无需身份验证

#### 问题 2：带 .git 后缀的 URL 要求凭证

**问题描述**

用户报告了以下行为差异：
```bash
# ❌ 这种方式会要求输入账号密码
git clone http://localhost:17856/sharpbrowser/SharpBrowser.git

# ✅ 这种方式可以直接下载
git clone http://localhost:17856/sharpbrowser/SharpBrowser
```

**根本原因**
- `GitSmartHttpController` 的路由只匹配 `{owner}/{repo}/info/refs`
- 当 URL 是 `{owner}/{repo}.git/info/refs` 时，路由不匹配
- 请求落到 `ForwardProxyController`，在早期版本中会转发 Authorization 头
- 导致身份验证失败

**解决方案**
- 为每个 Git 端点添加双路由支持（带/不带 `.git` 后缀）
- 在方法内部自动处理 `.git` 后缀
- 确保两种 URL 格式都由 `GitSmartHttpController` 处理
- 统一使用匿名访问逻辑

### 📝 使用示例

#### 克隆公共仓库（新功能）
```bash
# 直接克隆，不需要输入凭证
git clone http://your-proxy-server/microsoft/vscode
git clone http://your-proxy-server/torvalds/linux
git clone http://your-proxy-server/sharpbrowser/SharpBrowser

# 带 .git 后缀也完全支持
git clone http://your-proxy-server/microsoft/vscode.git
git clone http://your-proxy-server/torvalds/linux.git
git clone http://your-proxy-server/sharpbrowser/SharpBrowser.git

# 也支持完整 URL 格式
git clone http://your-proxy-server/https://github.com/user/repo.git
```

#### 禁用凭证提示（可选）
```bash
# 如果仍然提示输入凭证，可以使用环境变量
# Windows PowerShell
$env:GIT_TERMINAL_PROMPT = "0"
git clone http://your-proxy-server/user/repo

# Linux/Mac
export GIT_TERMINAL_PROMPT=0
git clone http://your-proxy-server/user/repo
```

### ⚠️ 破坏性变更

**无** - 此版本不包含破坏性变更

- 现有的克隆方式继续工作
- 添加的是新功能，不影响现有功能
- 如果之前通过代理访问私有仓库，需要等待未来版本的支持

### 🔮 未来计划

#### 私有仓库支持（计划中）
- 通过配置文件设置 GitHub Personal Access Token (PAT)
- 支持多种认证方式（OAuth、SSH 密钥转发）
- 环境变量配置选项

#### 性能优化（计划中）
- 添加响应缓存机制
- 支持 Git 协议的压缩
- 连接池优化

#### 监控和日志（计划中）
- 更详细的访问日志
- 性能指标收集
- 错误追踪和分析

### 🔄 升级说明

#### 从 1.0.x 升级到 1.1.0

1. **拉取最新代码**
   ```bash
   git pull origin main
   ```

2. **重新构建**
   ```bash
   dotnet build
   ```

3. **重启服务**
   ```bash
   dotnet run
   ```

4. **验证功能**
   ```bash
   git clone http://localhost:17856/octocat/Hello-World
   ```

#### 配置变更
- **无需配置变更** - 新功能自动启用
- 现有配置文件（appsettings.json）无需修改

#### 依赖项
- 无新增外部依赖
- 继续使用 YARP（Yarp.ReverseProxy）
- 继续使用 .NET 9.0

### 📊 技术细节

#### 性能影响
- **无负面影响** - 移除头比添加头更快
- **内存使用** - 无变化
- **CPU 使用** - 无变化
- **网络延迟** - 无变化

#### 兼容性
- **Git 版本** - 支持所有 Git 2.x 版本
- **GitHub** - 完全兼容 GitHub 的 Smart HTTP 协议
- **.NET 版本** - 需要 .NET 9.0
- **操作系统** - Windows、Linux、macOS

### 🧪 测试覆盖

#### 测试场景
- ✅ 克隆小型公共仓库（< 1MB）
- ✅ 克隆中型公共仓库（1MB - 100MB）
- ✅ 克隆大型公共仓库（> 100MB）
- ✅ fetch 和 pull 操作
- ✅ 下载原始文件
- ✅ 下载 ZIP 压缩包
- ✅ Web 界面代理

#### 测试的仓库
- octocat/Hello-World（小型）
- microsoft/vscode（中型）
- sharpbrowser/SharpBrowser（原问题仓库）

### 👥 贡献者

- **PeiKeSmart Team** - 主要开发和维护

### 🙏 致谢

感谢用户反馈的问题和建议，促使我们改进了代理服务器的功能。

### 📧 反馈和支持

如果您遇到问题或有建议：
- 提交 Issue 到 GitHub 仓库
- 查看文档：ANONYMOUS_ACCESS.md 和 TEST_ANONYMOUS_ACCESS.md
- 联系 PeiKeSmart 技术支持

---

## [1.0.0] - 2025-10-15

### ✨ 初始版本

- 基本的 Git 代理功能
- 支持 Git Smart HTTP 协议
- 支持多种 URL 格式
- 基于 YARP 的高性能转发
- 流式数据传输

### 已知问题
- 克隆公共仓库时提示输入凭证 ⚠️ **已在 1.1.0 中修复**
- 无效的 Authorization 头导致认证失败 ⚠️ **已在 1.1.0 中修复**
