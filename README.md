# Git Proxy Cloner

一个用于代理 GitHub 仓库克隆的 ASP.NET Core 应用程序，支持通过代理服务器加速 Git 操作。

## ✨ 核心特性

- ✅ **匿名访问公共仓库** - 无需 GitHub 账号和密码
- ✅ **支持 .git 后缀** - `user/repo` 和 `user/repo.git` 两种格式都支持
- ✅ **Git Smart HTTP 协议** - 完整支持 clone、fetch、push 操作
- ✅ **流式传输** - 高效处理大型仓库
- ✅ **多种 URL 格式** - 灵活的访问方式

## 🚀 快速开始

### 克隆仓库

```bash
# 简化格式（推荐）
git clone http://proxy-server/microsoft/vscode

# 带 .git 后缀
git clone http://proxy-server/microsoft/vscode.git

# 完整 GitHub URL
git clone http://proxy-server/https://github.com/microsoft/vscode.git
```

### 本地运行

```bash
# 克隆项目
git clone https://github.com/your-org/DH.GitProxyCloner
cd DH.GitProxyCloner

# 运行
dotnet run --project src/DH.GitProxyCloner

# 访问 http://localhost:17856
```

## 📚 文档

- [故障排除](./doc/TROUBLESHOOTING.md) - 常见问题和解决方案
- [变更日志](./CHANGELOG.md) - 版本历史和更新说明

## ⚙️ 部署

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY bin/Release/net9.0/publish /app
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "DH.GitProxyCloner.dll"]
```

### Nginx 反向代理

```nginx
server {
    listen 80;
    server_name git-proxy.example.com;
    
    location / {
        proxy_pass http://localhost:17856;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_buffering off;
    }
}
```

## 🔧 技术栈

- .NET 9.0
- ASP.NET Core
- YARP (Yet Another Reverse Proxy)

## 📝 许可证

MIT License

---

**技术支持：** PeiKeSmart © 2024

