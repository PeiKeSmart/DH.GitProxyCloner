# Git Proxy Cloner

一个用于代理 GitHub 仓库克隆的 ASP.NET Core 应用程序，支持通过代理服务器加速 Git 操作和 GitHub 网页浏览。

## 功能特性

- 支持 Git Smart HTTP 协议
- 代理 `git clone`、`git fetch`、`git push` 等操作
- **新增**: 支持 GitHub 网页代理访问
- **新增**: 支持搜索、仓库浏览、用户主页等功能
- 支持多种 URL 格式
- 流式数据传输，支持大型仓库
- 可配置的超时和缓存设置

## 使用方法

### Git 代理操作

#### 1. 简化格式
```bash
git clone http://your-domain.com/user/repo
```

#### 2. 完整 GitHub URL 格式
```bash
git clone http://your-domain.com/https://github.com/user/repo.git
```

#### 3. 带路径的完整格式
```bash
git clone http://your-domain.com/user/repo.git
```

### GitHub 网页代理 (新增功能)

#### 1. 访问代理首页
```
http://your-domain.com/web/
```

#### 2. GitHub 搜索
```bash
# 搜索代码
http://your-domain.com/web/search?q=repo%3AOwner%2FRepo%20keyword&type=code

# 搜索仓库
http://your-domain.com/web/search?q=keyword&type=repositories

# 搜索用户
http://your-domain.com/web/search?q=username&type=users
```

#### 3. 浏览仓库和用户
```bash
# 仓库主页
http://your-domain.com/web/user/repo

# 用户主页
http://your-domain.com/web/username

# GitHub Trending
http://your-domain.com/web/trending
```

详细的 Web 代理使用说明请参考：[GitHub Web 代理指南](GitHub-Web-Proxy-Guide.md)

## 实现原理

### Git Smart HTTP 协议
Git 使用 HTTP/HTTPS 协议进行远程操作，主要涉及以下端点：

1. **信息发现** (`/info/refs?service=git-upload-pack`)
   - 客户端首先请求这个端点获取仓库信息
   - 服务器返回可用的引用（分支、标签等）

2. **数据传输** (`/git-upload-pack`)
   - 用于 `git clone`、`git fetch` 等下载操作
   - 客户端发送想要的对象，服务器返回打包的数据

3. **推送操作** (`/git-receive-pack`)
   - 用于 `git push` 操作
   - 客户端上传新的对象到服务器

### 代理实现
本应用作为中间代理，执行以下操作：

1. **URL 解析**: 将代理 URL 转换为实际的 GitHub URL
2. **请求转发**: 将客户端的 Git 请求转发到 GitHub
3. **响应代理**: 将 GitHub 的响应流式传输回客户端
4. **协议保持**: 保持 Git Smart HTTP 协议的完整性

### 关键技术点

#### 1. 流式传输
```csharp
// 使用流式传输避免内存占用过大
await response.Content.CopyToAsync(HttpContext.Response.Body);
```

#### 2. 请求头处理
```csharp
// 复制重要的 Git 相关请求头
var importantHeaders = new[]
{
    "Authorization", "User-Agent", "Accept", 
    "Accept-Encoding", "Content-Type", "Git-Protocol"
};
```

#### 3. 内容类型处理
- `application/x-git-upload-pack-request` - 上传包请求
- `application/x-git-upload-pack-result` - 上传包响应
- `application/x-git-receive-pack-request` - 接收包请求
- `application/x-git-receive-pack-result` - 接收包响应

## 配置说明

在 `appsettings.json` 中配置：

```json
{
  "GitProxy": {
    "EnableCaching": false,
    "CacheExpirationMinutes": 30,
    "MaxRequestSizeMB": 100,
    "TimeoutMinutes": 10,
    "AllowedDomains": ["github.com"],
    "UserAgent": "git/2.0.0 (GitProxyCloner/1.0)"
  }
}
```

## 部署建议

### 1. Docker 部署
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY bin/Release/net9.0/publish/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "DH.GitProxyCloner.dll"]
```

### 2. 反向代理配置 (Nginx)
```nginx
location / {
    proxy_pass http://localhost:5000;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_buffering off;
    proxy_request_buffering off;
}
```

## 注意事项

1. **安全性**: 如果需要访问私有仓库，需要处理 GitHub 的认证
2. **性能**: 对于大型仓库，考虑启用缓存机制
3. **限制**: GitHub 有 API 速率限制，高频使用时需要注意
4. **HTTPS**: 生产环境建议使用 HTTPS

## 扩展功能

### 认证支持
可以添加 GitHub Token 支持来访问私有仓库：

```csharp
if (HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
{
    request.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
}
```

### 缓存机制
对于频繁访问的仓库，可以实现缓存：

```csharp
// 使用 IMemoryCache 或 Redis 缓存 Git 对象
public class GitObjectCache
{
    // 实现缓存逻辑
}
```

### 监控和日志
添加详细的请求日志和性能监控：

```csharp
_logger.LogInformation("Git operation: {Method} {Url} - {Duration}ms", 
    method, url, duration);
```

## 故障排除

### 常见问题

1. **克隆失败**: 检查 URL 格式是否正确
2. **超时**: 调整 `TimeoutMinutes` 配置
3. **认证失败**: 确保 GitHub Token 有正确的权限

### 调试技巧

启用详细日志：
```json
{
  "Logging": {
    "LogLevel": {
      "DH.GitProxyCloner": "Debug"
    }
  }
}
```
