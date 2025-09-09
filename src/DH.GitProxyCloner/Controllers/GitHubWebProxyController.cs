using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Text;

namespace DH.GitProxyCloner.Controllers;

/// <summary>
/// GitHub 网页代理控制器，用于代理访问 GitHub 的各种页面
/// </summary>
[ApiController]
[Route("web/{*path}")]
public class GitHubWebProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubWebProxyController> _logger;

    public GitHubWebProxyController(HttpClient httpClient, ILogger<GitHubWebProxyController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// 代理所有 GitHub 网页请求
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> ProxyGitHubWeb()
    {
        var requestPath = HttpContext.Request.Path.Value?.Replace("/web/", "").TrimStart('/');
        var queryString = HttpContext.Request.QueryString.Value;

        if (string.IsNullOrEmpty(requestPath))
        {
            return HandleWebRootRequest();
        }

        // 构建完整的 GitHub URL
        var githubUrl = $"https://github.com/{requestPath}";
        var fullTargetUrl = $"{githubUrl}{queryString}";

        _logger.LogInformation($"Proxying GitHub web request to: {fullTargetUrl}");

        return await ProxyToGitHub(fullTargetUrl);
    }

    /// <summary>
    /// 处理根路径的请求
    /// </summary>
    private IActionResult HandleWebRootRequest()
    {
        var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>GitHub Web Proxy</title>
    <meta charset='utf-8'>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif; margin: 40px; line-height: 1.6; }
        .container { max-width: 800px; margin: 0 auto; }
        .example { background: #f6f8fa; padding: 16px; border-radius: 8px; margin: 16px 0; }
        code { background: #f6f8fa; padding: 2px 6px; border-radius: 4px; font-family: 'Monaco', 'Consolas', monospace; }
        h1 { color: #0366d6; }
        h2 { color: #586069; margin-top: 32px; }
        .usage-type { margin: 24px 0; }
        .search-form { background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; }
        .search-form input[type='text'] { width: 70%; padding: 8px; border: 1px solid #d1d5da; border-radius: 4px; }
        .search-form button { padding: 8px 16px; background: #0366d6; color: white; border: none; border-radius: 4px; cursor: pointer; }
        .search-form button:hover { background: #0256cc; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🌐 GitHub Web Proxy</h1>
        <p>通过代理访问 GitHub 的各种页面，包括搜索、仓库浏览等。</p>
        
        <div class='search-form'>
            <h3>🔍 快速搜索</h3>
            <form id='searchForm'>
                <input type='text' id='searchQuery' placeholder='输入搜索关键词或直接输入 GitHub 路径...' />
                <button type='submit'>搜索</button>
            </form>
            <p><small>例如：搜索 'netcore' 或直接输入 'search?q=netcore&type=code'</small></p>
        </div>
        
        <h2>📖 使用方法</h2>
        
        <div class='usage-type'>
            <h3>搜索代码：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/search?q=repo%3APeiKeSmart%2FDH.FrameWork%20netcore&type=code</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>浏览仓库：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/PeiKeSmart/DH.FrameWork</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>查看用户主页：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/PeiKeSmart</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>搜索仓库：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/search?q=DH.FrameWork&type=repositories</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>搜索用户：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/search?q=PeiKeSmart&type=users</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>GitHub Trending：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/trending</code>
            </div>
        </div>
        
        <h2>🎯 支持的页面类型</h2>
        <ul>
            <li><strong>搜索页面</strong>：代码搜索、仓库搜索、用户搜索等</li>
            <li><strong>仓库页面</strong>：仓库首页、文件浏览、提交历史等</li>
            <li><strong>用户页面</strong>：用户主页、仓库列表等</li>
            <li><strong>趋势页面</strong>：GitHub Trending</li>
            <li><strong>Issues/PR页面</strong>：问题和拉取请求</li>
            <li><strong>其他GitHub页面</strong>：几乎所有公开的GitHub页面</li>
        </ul>
        
        <p><small>注意：此代理主要用于访问公开内容，私有仓库需要适当的身份验证。</small></p>
    </div>
    
    <script>
        document.getElementById('searchForm').addEventListener('submit', function(e) {
            e.preventDefault();
            const query = document.getElementById('searchQuery').value.trim();
            if (query) {
                // 检查是否是直接的 GitHub 路径
                if (query.includes('search?') || query.includes('/') || query.includes('=')) {
                    // 直接路径
                    window.location.href = '" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/' + query;
                } else {
                    // 简单搜索
                    window.location.href = '" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/web/search?q=' + encodeURIComponent(query) + '&type=code';
                }
            }
        });
    </script>
</body>
</html>";
        return Content(html, "text/html");
    }

    /// <summary>
    /// 代理请求到 GitHub
    /// </summary>
    private async Task<IActionResult> ProxyToGitHub(string targetUrl)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(HttpContext.Request.Method), targetUrl);

            // 复制重要的请求头
            CopyImportantHeaders(request);

            // 复制请求体（如果是POST请求）
            if (HttpContext.Request.Method == "POST")
            {
                await CopyRequestBody(request);
            }

            // 发送请求
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 设置响应状态码
            HttpContext.Response.StatusCode = (int)response.StatusCode;

            // 复制响应头
            SetResponseHeaders(response);

            // 直接流式复制响应体，不修改内容
            await response.Content.CopyToAsync(HttpContext.Response.Body);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error proxying to {targetUrl}");
            return StatusCode(500, $"Proxy error: {ex.Message}");
        }
    }

    /// <summary>
    /// 复制重要的请求头
    /// </summary>
    private void CopyImportantHeaders(HttpRequestMessage request)
    {
        var importantHeaders = new[]
        {
            "Accept",
            "Accept-Encoding", 
            "Accept-Language",
            "Cache-Control",
            "User-Agent",
            "Authorization",
            "Cookie"
        };

        foreach (var headerName in importantHeaders)
        {
            if (HttpContext.Request.Headers.TryGetValue(headerName, out var values))
            {
                try
                {
                    // 修改 User-Agent 以避免被 GitHub 识别为爬虫
                    if (headerName == "User-Agent")
                    {
                        var userAgent = values.FirstOrDefault() ?? "";
                        if (!userAgent.Contains("GitHubWebProxy"))
                        {
                            userAgent = userAgent.Contains("Mozilla") ? userAgent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 GitHubWebProxy/1.0";
                        }
                        request.Headers.TryAddWithoutValidation(headerName, userAgent);
                    }
                    else
                    {
                        request.Headers.TryAddWithoutValidation(headerName, values.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Failed to copy header: {headerName}");
                }
            }
        }

        // 添加一些必要的头部
        request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
    }

    /// <summary>
    /// 复制请求体
    /// </summary>
    private async Task CopyRequestBody(HttpRequestMessage request)
    {
        if (HttpContext.Request.ContentLength > 0)
        {
            var contentType = HttpContext.Request.ContentType ?? "application/x-www-form-urlencoded";
            
            var bodyStream = new MemoryStream();
            await HttpContext.Request.Body.CopyToAsync(bodyStream);
            bodyStream.Position = 0;
            
            request.Content = new StreamContent(bodyStream);
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            
            if (HttpContext.Request.ContentLength.HasValue)
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Length", 
                    HttpContext.Request.ContentLength.Value.ToString());
            }
        }
    }

    /// <summary>
    /// 设置响应头
    /// </summary>
    private void SetResponseHeaders(HttpResponseMessage response)
    {
        // 复制重要的响应头
        foreach (var header in response.Headers)
        {
            if (ShouldCopyResponseHeader(header.Key))
            {
                try
                {
                    HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Failed to copy response header: {header.Key}");
                }
            }
        }

        foreach (var header in response.Content.Headers)
        {
            if (ShouldCopyResponseHeader(header.Key))
            {
                try
                {
                    HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Failed to copy content header: {header.Key}");
                }
            }
        }
    }

    /// <summary>
    /// 判断是否应该复制响应头
    /// </summary>
    private static bool ShouldCopyResponseHeader(string headerName)
    {
        var skipHeaders = new[]
        {
            "transfer-encoding", "connection", "upgrade", "server"
        };
        return !skipHeaders.Contains(headerName.ToLowerInvariant());
    }
}
