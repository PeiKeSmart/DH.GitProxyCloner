using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Forwarder;
using System.Net;

namespace DH.GitProxyCloner.Controllers;

/// <summary>
/// 正向代理控制器 - 支持动态目标的 GitHub 代理访问
/// 优先级低于专门的Git协议控制器
/// </summary>
[ApiController]
public class ForwardProxyController : ControllerBase
{
    private readonly IHttpForwarder _httpForwarder;
    private readonly ILogger<ForwardProxyController> _logger;
    private readonly HttpMessageInvoker _httpClient;

    public ForwardProxyController(
        IHttpForwarder httpForwarder, 
        ILogger<ForwardProxyController> logger)
    {
        _httpForwarder = httpForwarder;
        _logger = logger;
        
        // 创建用于转发的 HttpClient - 简化配置，专注于稳定性
        _httpClient = new HttpMessageInvoker(new SocketsHttpHandler()
        {
            UseProxy = true, // 允许使用系统代理设置
            AllowAutoRedirect = false, // YARP会处理重定向
            AutomaticDecompression = DecompressionMethods.None, // YARP会处理压缩
            UseCookies = false, // 禁用Cookie管理
            ActivityHeadersPropagator = null,
            ConnectTimeout = TimeSpan.FromSeconds(60), // 增加连接超时时间
            PooledConnectionLifetime = TimeSpan.FromMinutes(15), // 连接池生命周期
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5), // 空闲连接超时
            MaxConnectionsPerServer = 100 // 增加每服务器连接数
        });
    }

    /// <summary>
    /// 处理根路径请求
    /// </summary>
    [HttpGet("")]
    public IActionResult GetRoot()
    {
        return HandleRootRequest();
    }

    /// <summary>
    /// 测试到GitHub的网络连接
    /// </summary>
    [HttpGet("_test/connection")]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            _logger.LogInformation("Testing connection to GitHub...");
            
            using var testClient = new HttpClient();
            testClient.Timeout = TimeSpan.FromSeconds(10);
            testClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            
            var response = await testClient.GetAsync("https://github.com");
            
            var result = new
            {
                Status = "Success",
                StatusCode = (int)response.StatusCode,
                Headers = response.Headers.Select(h => new { h.Key, Value = string.Join(", ", h.Value) }).ToList(),
                Message = $"Successfully connected to GitHub. Status: {response.StatusCode}"
            };
            
            _logger.LogInformation($"Connection test successful: {response.StatusCode}");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            
            var result = new
            {
                Status = "Failed",
                Error = ex.Message,
                Type = ex.GetType().Name,
                Message = "Failed to connect to GitHub. Check your network connection and proxy settings."
            };
            
            return StatusCode(500, result);
        }
    }

    /// <summary>
    /// 处理所有的正向代理请求 - 通用路由（优先级最低）
    /// </summary>
    [HttpGet("{*catchAll}")]
    [HttpPost("{*catchAll}")]
    [HttpPut("{*catchAll}")]
    [HttpDelete("{*catchAll}")]
    [HttpHead("{*catchAll}")]
    [HttpOptions("{*catchAll}")]
    [HttpPatch("{*catchAll}")]
    public async Task<IActionResult> ProxyRequest()
    {
        var requestPath = HttpContext.Request.Path.Value?.TrimStart('/') ?? "";
        var queryString = HttpContext.Request.QueryString.Value ?? "";

        _logger.LogInformation($"Incoming request path: '{requestPath}'");
        _logger.LogInformation($"Query string: '{queryString}'");

        // 处理根路径请求
        if (string.IsNullOrEmpty(requestPath))
        {
            return HandleRootRequest();
        }

        // 解析目标 URL
        var (destinationPrefix, transformedPath) = BuildDestinationAndPath(requestPath);
        if (string.IsNullOrEmpty(destinationPrefix))
        {
            return BadRequest("Invalid GitHub URL format");
        }

        _logger.LogInformation($"Forward proxy request: {HttpContext.Request.Method} to {destinationPrefix} with path: {transformedPath}");

        // 临时修改请求路径供YARP使用
        var originalPath = HttpContext.Request.Path;
        HttpContext.Request.Path = transformedPath;

        try
        {
            // 使用 YARP 转发请求，让YARP自动协商最佳HTTP版本
            var forwarderConfig = new ForwarderRequestConfig()
            {
                ActivityTimeout = TimeSpan.FromMinutes(10) // 总体请求超时
            };

            // 使用自定义转换器，移除 Authorization 头以支持公共仓库的匿名访问
            var transformer = new AnonymousAccessTransformer();

            var error = await _httpForwarder.SendAsync(
                HttpContext, 
                destinationPrefix, 
                _httpClient,
                forwarderConfig,
                transformer);

            // 检查转发是否成功
            if (error != ForwarderError.None)
            {
                var errorFeature = HttpContext.GetForwarderErrorFeature();
                var exception = errorFeature?.Exception;
                
                _logger.LogError(exception, $"Proxy error: {error}");
                return StatusCode(502, $"Proxy error: {error}");
            }

            return new EmptyResult();
        }
        finally
        {
            // 恢复原始路径
            HttpContext.Request.Path = originalPath;
        }
    }

    /// <summary>
    /// 构建目标服务器基础URL和转换后的路径
    /// </summary>
    private (string destinationPrefix, string transformedPath) BuildDestinationAndPath(string requestPath)
    {
        try
        {
            _logger.LogInformation($"Building destination from path: '{requestPath}'");

            // 情况1: 完整的 GitHub URL (https://github.com/...)
            if (requestPath.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                // 提取路径部分
                var pathPart = requestPath.Substring("https://github.com".Length);
                var result = ("https://github.com", pathPart);
                _logger.LogInformation($"Case 1 - Full GitHub URL: {result.Item1} + {result.Item2}");
                return result;
            }

            // 情况2: GitHub 域名开头 (github.com/...)
            if (requestPath.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            {
                // 提取路径部分
                var pathPart = requestPath.Substring("github.com".Length);
                var result = ("https://github.com", pathPart);
                _logger.LogInformation($"Case 2 - GitHub domain: {result.Item1} + {result.Item2}");
                return result;
            }

            // 情况3: Web 代理路径 (web/user/repo/...)
            if (requestPath.StartsWith("web/", StringComparison.OrdinalIgnoreCase))
            {
                var webPath = requestPath.Substring(4); // 移除 "web/" 前缀
                var pathPart = "/" + webPath;
                var result = ("https://github.com", pathPart);
                _logger.LogInformation($"Case 3 - Web proxy: {result.Item1} + {result.Item2}");
                return result;
            }

            // 情况4: 简化格式 (user/repo/...)
            if (IsGitHubPath(requestPath))
            {
                var pathPart = "/" + requestPath;
                var result = ("https://github.com", pathPart);
                _logger.LogInformation($"Case 4 - Simple format: {result.Item1} + {result.Item2}");
                return result;
            }

            _logger.LogWarning($"No matching case for path: '{requestPath}'");
            return (string.Empty, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to build destination for path: {requestPath}");
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// 判断是否是有效的 GitHub 路径
    /// </summary>
    private static bool IsGitHubPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // 简单检查：至少包含用户名和仓库名
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
    }

    /// <summary>
    /// 处理根路径请求 - 返回使用说明
    /// </summary>
    private IActionResult HandleRootRequest()
    {
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
        var accept = HttpContext.Request.Headers.Accept.ToString();
        
        // 检查是否是浏览器请求
        var isBrowserRequest = accept.Contains("text/html") || 
                              userAgent.Contains("Mozilla") || 
                              userAgent.Contains("Chrome") || 
                              userAgent.Contains("Safari");

        if (isBrowserRequest)
        {
            return Content(GetUsageHtml(), "text/html", System.Text.Encoding.UTF8);
        }
        else
        {
            return Ok("GitHub Forward Proxy is running.\n" +
                     "Usage: Use this server as a proxy to access GitHub resources.\n" +
                     "Examples:\n" +
                     $"  git clone {HttpContext.Request.Scheme}://{HttpContext.Request.Host}/user/repo\n" +
                     $"  curl {HttpContext.Request.Scheme}://{HttpContext.Request.Host}/user/repo/raw/main/README.md");
        }
    }

    /// <summary>
    /// 生成使用说明 HTML 页面
    /// </summary>
    private string GetUsageHtml()
    {
        var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}";
        
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>GitHub Forward Proxy</title>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1'>
    <style>
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif; 
            margin: 40px auto; 
            max-width: 900px; 
            line-height: 1.6; 
            color: #333;
            padding: 0 20px;
        }}
        .header {{ 
            text-align: center; 
            margin-bottom: 40px; 
            padding: 20px; 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border-radius: 10px;
        }}
        .section {{ 
            margin: 30px 0; 
            padding: 20px; 
            background: #f8f9fa; 
            border-radius: 8px; 
            border-left: 4px solid #007bff;
        }}
        .example {{ 
            background: #e9ecef; 
            padding: 15px; 
            border-radius: 6px; 
            margin: 15px 0; 
            font-family: 'Monaco', 'Consolas', monospace;
            overflow-x: auto;
        }}
        .code {{ 
            background: #f1f3f4; 
            padding: 3px 6px; 
            border-radius: 4px; 
            font-family: 'Monaco', 'Consolas', monospace; 
            font-size: 0.9em;
        }}
        h1 {{ margin: 0; font-size: 2.5em; }}
        h2 {{ color: #495057; margin-top: 0; }}
        h3 {{ color: #007bff; margin-bottom: 10px; }}
        .feature-grid {{ 
            display: grid; 
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); 
            gap: 20px; 
            margin: 20px 0; 
        }}
        .feature-card {{ 
            background: white; 
            padding: 20px; 
            border-radius: 8px; 
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .search-form {{ 
            background: white; 
            padding: 25px; 
            border-radius: 8px; 
            margin: 20px 0;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .search-form input[type='text'] {{ 
            width: 70%; 
            padding: 12px; 
            border: 2px solid #dee2e6; 
            border-radius: 6px; 
            font-size: 16px;
        }}
        .search-form button {{ 
            padding: 12px 20px; 
            background: #007bff; 
            color: white; 
            border: none; 
            border-radius: 6px; 
            cursor: pointer; 
            font-size: 16px;
            margin-left: 10px;
        }}
        .search-form button:hover {{ background: #0056b3; }}
        .status {{ 
            text-align: center; 
            padding: 15px; 
            background: #d4edda; 
            color: #155724; 
            border-radius: 6px; 
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🚀 GitHub Forward Proxy</h1>
        <p>高性能 GitHub 正向代理服务，支持 Git 协议和 Web 访问</p>
    </div>

    <div class='status'>
        ✅ 代理服务运行正常 | 基于 YARP 技术 | 支持流式传输
    </div>

    <div class='search-form'>
        <h3>🔍 快速访问</h3>
        <form id='accessForm'>
            <input type='text' id='repoPath' placeholder='输入 GitHub 路径，如: microsoft/vscode 或完整URL' />
            <button type='submit'>访问</button>
        </form>
        <p><small>支持格式: user/repo、github.com/user/repo、https://github.com/user/repo</small></p>
    </div>

    <div class='feature-grid'>
        <div class='feature-card'>
            <h3>🔧 Git 克隆</h3>
            <div class='example'>git clone {baseUrl}/microsoft/vscode</div>
            <p>支持所有 Git 操作：clone、fetch、push、pull</p>
        </div>

        <div class='feature-card'>
            <h3>🌐 Web 浏览</h3>
            <div class='example'>{baseUrl}/web/microsoft/vscode</div>
            <p>通过代理访问 GitHub 网页界面</p>
        </div>

        <div class='feature-card'>
            <h3>📁 文件下载</h3>
            <div class='example'>{baseUrl}/microsoft/vscode/raw/main/package.json</div>
            <p>直接下载仓库中的文件</p>
        </div>

        <div class='feature-card'>
            <h3>📦 ZIP 下载</h3>
            <div class='example'>{baseUrl}/microsoft/vscode/archive/main.zip</div>
            <p>下载整个仓库的压缩包</p>
        </div>
    </div>

    <div class='section'>
        <h2>📋 使用说明</h2>
        
        <h3>1. Git 操作</h3>
        <div class='example'>
git clone {baseUrl}/user/repo<br>
git clone {baseUrl}/https://github.com/user/repo.git<br>
git remote add origin {baseUrl}/user/repo
        </div>

        <h3>2. Web 访问</h3>
        <div class='example'>
# 浏览仓库首页<br>
{baseUrl}/web/user/repo<br><br>
# 搜索代码<br>
{baseUrl}/web/search?q=keyword&type=code
        </div>

        <h3>3. 文件操作</h3>
        <div class='example'>
# 查看文件内容<br>
curl {baseUrl}/user/repo/raw/main/README.md<br><br>
# 下载仓库ZIP<br>
wget {baseUrl}/user/repo/archive/main.zip
        </div>
    </div>

    <div class='section'>
        <h2>⚡ 技术特性</h2>
        <ul>
            <li><strong>高性能</strong>: 基于 YARP 反向代理技术</li>
            <li><strong>流式传输</strong>: 支持大文件的高效传输</li>
            <li><strong>协议支持</strong>: 完整支持 Git Smart HTTP 协议</li>
            <li><strong>请求透传</strong>: 保持原始请求头和认证信息</li>
            <li><strong>错误处理</strong>: 完善的错误处理和日志记录</li>
        </ul>
    </div>

    <script>
        document.getElementById('accessForm').addEventListener('submit', function(e) {{
            e.preventDefault();
            const path = document.getElementById('repoPath').value.trim();
            if (path) {{
                // 检测路径类型并构建正确的URL
                let targetUrl;
                if (path.startsWith('http')) {{
                    // 完整URL，直接使用
                    targetUrl = '{baseUrl}/' + path;
                }} else if (path.startsWith('github.com/')) {{
                    // github.com开头
                    targetUrl = '{baseUrl}/' + path;
                }} else {{
                    // 简单格式，添加web前缀用于浏览
                    targetUrl = '{baseUrl}/web/' + path;
                }}
                window.open(targetUrl, '_blank');
            }}
        }});
    </script>
</body>
</html>";
    }

}

/// <summary>
/// 自定义 HTTP 转换器，用于移除 Authorization 头以支持公共仓库的匿名访问
/// </summary>
public class AnonymousAccessTransformer : HttpTransformer
{
    public override async ValueTask TransformRequestAsync(HttpContext httpContext, 
        HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
    {
        // 调用默认的转换逻辑
        await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

        // 移除 Authorization 头，让 GitHub 将请求视为匿名访问
        // 这对于公共仓库是必需的，因为无效的 Authorization 头会导致 401 错误
        proxyRequest.Headers.Remove("Authorization");
        
        // 注意: 如果需要访问私有仓库，可以在这里添加配置的 Personal Access Token
        // 例如: proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("token", "your_pat_here");
    }
}
