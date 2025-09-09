# GitHub Web 代理功能

## 概述

除了原有的 Git 仓库代理功能外，现在增加了 GitHub 网页代理功能，可以通过代理访问 GitHub 的各种页面，包括搜索、仓库浏览、用户主页等。

## 新增功能

### GitHub Web 代理
- **路由前缀**: `/web/`
- **功能**: 代理访问 GitHub 的所有公开页面
- **特点**: 
  - 自动修改 HTML 中的链接，使其通过代理访问
  - 添加代理提示横幅
  - 支持搜索、浏览、趋势页面等

## 使用方法

### 1. 访问代理首页
```
http://localhost:17856/web/
```

### 2. GitHub 搜索
```bash
# 搜索代码
http://localhost:17856/web/search?q=repo%3APeiKeSmart%2FDH.FrameWork%20netcore&type=code

# 搜索仓库
http://localhost:17856/web/search?q=DH.FrameWork&type=repositories

# 搜索用户
http://localhost:17856/web/search?q=PeiKeSmart&type=users
```

### 3. 浏览仓库
```bash
# 仓库主页
http://localhost:17856/web/PeiKeSmart/DH.FrameWork

# 文件浏览
http://localhost:17856/web/PeiKeSmart/DH.FrameWork/blob/main/README.md

# 提交历史
http://localhost:17856/web/PeiKeSmart/DH.FrameWork/commits/main
```

### 4. 用户主页
```bash
http://localhost:17856/web/PeiKeSmart
```

### 5. GitHub Trending
```bash
# 趋势页面
http://localhost:17856/web/trending

# 特定语言的趋势
http://localhost:17856/web/trending/csharp
```

### 6. Issues 和 Pull Requests
```bash
# 仓库的 Issues
http://localhost:17856/web/PeiKeSmart/DH.FrameWork/issues

# 特定 Issue
http://localhost:17856/web/PeiKeSmart/DH.FrameWork/issues/1

# Pull Requests
http://localhost:17856/web/PeiKeSmart/DH.FrameWork/pulls
```

## 技术实现

### 1. 新增控制器
- `GitHubWebProxyController`: 专门处理 GitHub 网页代理

### 2. 主要功能
- **链接重写**: 自动将 HTML 中的 GitHub 链接重写为代理链接
- **请求头处理**: 模拟真实浏览器的请求头
- **响应修改**: 在页面顶部添加代理提示
- **表单处理**: 支持搜索表单等交互功能

### 3. 路由配置
```csharp
[Route("web/{*path}")]
public class GitHubWebProxyController : ControllerBase
```

## 注意事项

1. **访问限制**: 只能访问 GitHub 的公开内容
2. **功能限制**: 某些需要 JavaScript 的动态功能可能受限
3. **身份验证**: 私有仓库需要适当的身份验证
4. **性能**: 代理会增加一定的延迟
5. **合规性**: 请遵守 GitHub 的使用条款

## 示例场景

### 场景 1: 搜索特定仓库中的代码
```
原 URL: https://github.com/search?q=repo%3APeiKeSmart%2FDH.FrameWork%20netcore&type=code
代理 URL: http://localhost:17856/web/search?q=repo%3APeiKeSmart%2FDH.FrameWork%20netcore&type=code
```

### 场景 2: 浏览热门仓库
```
原 URL: https://github.com/trending
代理 URL: http://localhost:17856/web/trending
```

### 场景 3: 查看用户的所有仓库
```
原 URL: https://github.com/PeiKeSmart?tab=repositories
代理 URL: http://localhost:17856/web/PeiKeSmart?tab=repositories
```

## 与原有功能的区别

| 功能 | 原有 Git 代理 | 新增 Web 代理 |
|------|---------------|---------------|
| 用途 | Git 命令行操作 | 浏览器访问 |
| 路由 | `/{owner}/{repo}` | `/web/{path}` |
| 内容 | Git 协议数据 | HTML 页面 |
| 修改 | 透明代理 | 链接重写 |

## 配置

在 `Program.cs` 中已自动配置：
```csharp
builder.Services.AddHttpClient<GitHubWebProxyController>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 ...");
});
```

这样，你的 Git 代理服务现在既支持 Git 命令行操作，也支持通过浏览器访问 GitHub 的各种页面！
