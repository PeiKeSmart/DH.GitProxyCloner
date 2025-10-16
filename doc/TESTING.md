# Git Proxy Cloner 测试说明

## 启动服务

```bash
cd f:\Project\DH.GitProxyCloner\src\DH.GitProxyCloner
dotnet run
```

服务默认运行在 `http://localhost:5000`

## 测试命令

### 1. 测试服务是否运行
```bash
curl http://localhost:5000
```

### 2. 测试简化格式克隆
```bash
git clone http://localhost:5000/stilleshan/ServerStatus
```

### 3. 测试完整 GitHub URL 格式
```bash
git clone http://localhost:5000/https://github.com/stilleshan/ServerStatus.git
```

### 4. 测试信息获取（模拟 git 客户端行为）
```bash
curl "http://localhost:5000/stilleshan/ServerStatus/info/refs?service=git-upload-pack"
```

### 5. 测试浏览器访问（HTML 首页）
```bash
curl -H "Accept: text/html" http://localhost:5000
```

### 6. 测试 ZIP 下载
```bash
curl -L -o repo.zip "http://localhost:5000/stilleshan/ServerStatus/archive/main.zip"
```

### 7. 测试原始文件下载
```bash
curl "http://localhost:5000/stilleshan/ServerStatus/raw/main/README.md"
```

### 8. 测试浏览器重定向
```bash
curl -I "http://localhost:5000/stilleshan/ServerStatus"
```

### 9. 测试发布版本下载
```bash
curl -L "http://localhost:5000/microsoft/vscode/releases/latest/download/VSCode-win32-x64.zip"
```

## 验证步骤

1. **启动服务**
   ```bash
   dotnet run --urls="http://localhost:5000"
   ```

2. **测试基本功能**
   ```bash
   # 在新的终端窗口中
   mkdir test-clone
   cd test-clone
   git clone http://localhost:5000/stilleshan/ServerStatus
   ```

3. **检查克隆结果**
   ```bash
   cd ServerStatus
   ls -la
   git log --oneline -5
   ```

## 生产部署

### Docker 部署
```bash
# 构建镜像
docker build -t git-proxy-cloner .

# 运行容器
docker run -p 80:8080 git-proxy-cloner
```

### 使用示例
部署后，用户可以这样使用：

```bash
# 原始 GitHub 地址
git clone https://github.com/stilleshan/ServerStatus.git

# 使用代理（假设部署在 gh.xmly.dev）
git clone https://gh.xmly.dev/stilleshan/ServerStatus
```
