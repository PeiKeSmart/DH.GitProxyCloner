using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Text;

namespace DH.GitProxyCloner.Controllers;

[ApiController]
public class GitSmartHttpController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitSmartHttpController> _logger;

    public GitSmartHttpController(HttpClient httpClient, ILogger<GitSmartHttpController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Git info/refs 请求
    [HttpGet("{owner}/{repo}/info/refs")]
    public async Task<IActionResult> GetInfoRefs(string owner, string repo, [FromQuery] string service)
    {
        var githubUrl = $"https://github.com/{owner}/{repo}.git/info/refs?service={service}";
        return await ProxyGitRequest(githubUrl);
    }

    // Git upload-pack 请求（用于git clone/fetch）
    [HttpPost("{owner}/{repo}/git-upload-pack")]
    public async Task<IActionResult> PostUploadPack(string owner, string repo)
    {
        var githubUrl = $"https://github.com/{owner}/{repo}.git/git-upload-pack";
        return await ProxyGitRequest(githubUrl);
    }

    // Git receive-pack 请求（用于git push）
    [HttpPost("{owner}/{repo}/git-receive-pack")]
    public async Task<IActionResult> PostReceivePack(string owner, string repo)
    {
        var githubUrl = $"https://github.com/{owner}/{repo}.git/git-receive-pack";
        return await ProxyGitRequest(githubUrl);
    }

    private async Task<IActionResult> ProxyGitRequest(string targetUrl)
    {
        try
        {
            _logger.LogInformation($"Proxying Git request to: {targetUrl}");

            // 创建代理请求
            using var request = new HttpRequestMessage(
                new HttpMethod(HttpContext.Request.Method), 
                targetUrl);

            // 复制重要的Git相关请求头
            CopyGitHeaders(request);

            // 处理请求体（对于POST请求）
            if (HttpContext.Request.Method == "POST")
            {
                await CopyRequestBody(request);
            }

            // 发送请求到GitHub
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 设置响应头和状态码
            SetResponseHeaders(response);

            // 流式复制响应体
            await response.Content.CopyToAsync(HttpContext.Response.Body);

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error proxying Git request to {targetUrl}");
            return StatusCode(500, "Git proxy error");
        }
    }

    private void CopyGitHeaders(HttpRequestMessage request)
    {
        var importantHeaders = new[]
        {
            "Authorization",
            "User-Agent",
            "Accept",
            "Accept-Encoding",
            "Content-Type",
            "Git-Protocol"
        };

        foreach (var headerName in importantHeaders)
        {
            if (HttpContext.Request.Headers.TryGetValue(headerName, out var values))
            {
                if (headerName == "Content-Type" && request.Content != null)
                {
                    // Content-Type 会在设置 Content 时自动处理
                    continue;
                }

                try
                {
                    request.Headers.TryAddWithoutValidation(headerName, values.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to copy header: {headerName}");
                }
            }
        }
    }

    private Task CopyRequestBody(HttpRequestMessage request)
    {
        if (HttpContext.Request.ContentLength > 0)
        {
            var contentType = HttpContext.Request.ContentType ?? "application/x-git-upload-pack-request";
            
            // 直接使用Request.Body，避免不必要的内存复制
            request.Content = new StreamContent(HttpContext.Request.Body);
            request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            
            if (HttpContext.Request.ContentLength.HasValue)
            {
                request.Content.Headers.TryAddWithoutValidation("Content-Length", 
                    HttpContext.Request.ContentLength.Value.ToString());
            }
        }
        return Task.CompletedTask;
    }

    private void SetResponseHeaders(HttpResponseMessage response)
    {
        // 复制重要的响应头
        foreach (var header in response.Headers)
        {
            if (IsImportantResponseHeader(header.Key))
            {
                HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }
        }

        foreach (var header in response.Content.Headers)
        {
            if (IsImportantResponseHeader(header.Key))
            {
                HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }
        }

        // 设置响应状态码
        HttpContext.Response.StatusCode = (int)response.StatusCode;
    }

    private static bool IsImportantResponseHeader(string headerName)
    {
        var disallowedHeaders = new[]
        {
            "transfer-encoding", "connection", "upgrade"
        };

        return !disallowedHeaders.Contains(headerName.ToLowerInvariant());
    }
}

// 通用代理控制器，处理任何格式的请求
[ApiController]
[Route("{*catchAll}")]
public class UniversalGitProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UniversalGitProxyController> _logger;

    public UniversalGitProxyController(HttpClient httpClient, ILogger<UniversalGitProxyController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> ProxyRequest()
    {
        var requestPath = HttpContext.Request.Path.Value?.TrimStart('/');
        var queryString = HttpContext.Request.QueryString.Value;

        if (string.IsNullOrEmpty(requestPath))
        {
            return HandleRootRequest();
        }

        // 尝试解析GitHub URL
        var githubUrl = ParseGithubUrl(requestPath);
        if (string.IsNullOrEmpty(githubUrl))
        {
            return BadRequest("Invalid GitHub repository URL format");
        }

        var fullTargetUrl = $"{githubUrl}{queryString}";
        _logger.LogInformation($"Universal proxy to: {fullTargetUrl}");

        // 检查是否是简单的仓库访问（浏览器直接重定向）
        if (IsSimpleRepoAccess(requestPath) && IsBrowserRequest())
        {
            return Redirect(githubUrl);
        }

        return await ProxyToGithub(fullTargetUrl);
    }

    private IActionResult HandleRootRequest()
    {
        if (IsBrowserRequest())
        {
            // 返回 HTML 首页
            var html = @"
<!DOCTYPE html>
<html>
<head>
    <title>Git Proxy Cloner</title>
    <meta charset='utf-8'>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif; margin: 40px; line-height: 1.6; }
        .container { max-width: 800px; margin: 0 auto; }
        .example { background: #f6f8fa; padding: 16px; border-radius: 8px; margin: 16px 0; }
        code { background: #f6f8fa; padding: 2px 6px; border-radius: 4px; font-family: 'Monaco', 'Consolas', monospace; }
        h1 { color: #0366d6; }
        h2 { color: #586069; margin-top: 32px; }
        .usage-type { margin: 24px 0; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🚀 Git Proxy Cloner</h1>
        <p>一个 GitHub 仓库代理服务，支持 Git 克隆和浏览器下载。</p>
        
        <h2>🔧 Git 克隆使用方法</h2>
        <div class='usage-type'>
            <h3>简化格式：</h3>
            <div class='example'>
                <code>git clone " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/user/repo</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>完整 GitHub URL：</h3>
            <div class='example'>
                <code>git clone " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/https://github.com/user/repo.git</code>
            </div>
        </div>
        
        <h2>🌐 浏览器下载使用方法</h2>
        <div class='usage-type'>
            <h3>下载仓库 ZIP：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/user/repo/archive/main.zip</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>下载原始文件：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/user/repo/raw/main/README.md</code>
            </div>
        </div>
        
        <div class='usage-type'>
            <h3>浏览仓库（重定向到 GitHub）：</h3>
            <div class='example'>
                <code>" + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/user/repo</code>
            </div>
        </div>
        
        <h2>📝 示例</h2>
        <div class='example'>
            <p><strong>克隆仓库：</strong></p>
            <code>git clone " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/microsoft/vscode</code><br><br>
            
            <p><strong>下载 ZIP：</strong></p>
            <code>wget " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/microsoft/vscode/archive/main.zip</code><br><br>
            
            <p><strong>下载文件：</strong></p>
            <code>curl " + HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + @"/microsoft/vscode/raw/main/package.json</code>
        </div>
    </div>
</body>
</html>";
            return Content(html, "text/html");
        }
        else
        {
            // Git 客户端返回纯文本
            return Ok("Git Proxy Cloner is running. Usage: clone with your-domain.com/user/repo or your-domain.com/https://github.com/user/repo.git");
        }
    }

    private bool IsBrowserRequest()
    {
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        var accept = HttpContext.Request.Headers["Accept"].ToString();
        
        // 检查是否是浏览器请求
        return accept.Contains("text/html") || 
               userAgent.Contains("Mozilla") || 
               userAgent.Contains("Chrome") || 
               userAgent.Contains("Safari") || 
               userAgent.Contains("Edge") || 
               userAgent.Contains("Firefox");
    }

    private bool IsSimpleRepoAccess(string requestPath)
    {
        // 检查是否是简单的 user/repo 访问（没有额外路径）
        var match = Regex.Match(requestPath, @"^[^/]+/[^/]+$");
        return match.Success;
    }

    private string ParseGithubUrl(string requestPath)
    {
        if (string.IsNullOrEmpty(requestPath))
            return string.Empty;

        // 格式1: https://github.com/user/repo.git/path...
        if (requestPath.StartsWith("https://github.com/"))
        {
            return requestPath;
        }

        // 格式2: user/repo/path...
        var match = Regex.Match(requestPath, @"^([^/]+/[^/]+)(.*)$");
        if (match.Success)
        {
            var userRepo = match.Groups[1].Value;
            var path = match.Groups[2].Value;
            
            // 检查是否是浏览器下载请求（archive, raw, releases 等）
            if (IsBrowserDownloadRequest(path))
            {
                // 浏览器下载请求不需要 .git 后缀
                return $"https://github.com/{userRepo}{path}";
            }
            
            // Git 协议请求需要 .git 后缀
            if (!userRepo.EndsWith(".git"))
            {
                userRepo += ".git";
            }
            
            return $"https://github.com/{userRepo}{path}";
        }

        return string.Empty;
    }

    private bool IsBrowserDownloadRequest(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // 检查是否是浏览器下载相关的路径
        var browserPaths = new[]
        {
            "/archive/",      // ZIP 下载: /archive/main.zip
            "/releases/",     // 发布版本下载
            "/raw/",          // 原始文件下载
            "/blob/",         // 文件查看（重定向到 GitHub）
            "/tree/",         // 目录查看（重定向到 GitHub）
            "/commits/",      // 提交历史查看
            "/issues/",       // 问题页面
            "/pull/",         // 拉取请求
            "/wiki/",         // Wiki 页面
            "/actions/",      // GitHub Actions
            "/security/",     // 安全页面
            "/pulse/",        // 统计页面
            "/graphs/"        // 图表页面
        };

        return browserPaths.Any(browserPath => path.StartsWith(browserPath));
    }

    private async Task<IActionResult> ProxyToGithub(string targetUrl)
    {
        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(HttpContext.Request.Method), targetUrl);

            // 复制重要的请求头
            CopyImportantHeaders(request);

            // 复制请求体（如果是POST请求）
            if (HttpContext.Request.Method == "POST")
            {
                await CopyRequestBodyToRequest(request);
            }

            // 发送请求
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 设置响应
            await SetProxyResponse(response);

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error proxying to {targetUrl}");
            return StatusCode(500, $"Proxy error: {ex.Message}");
        }
    }

    private void CopyImportantHeaders(HttpRequestMessage request)
    {
        var importantHeaders = new[]
        {
            "Authorization", "User-Agent", "Accept", "Accept-Encoding", "Git-Protocol"
        };

        foreach (var headerName in importantHeaders)
        {
            if (HttpContext.Request.Headers.TryGetValue(headerName, out var values))
            {
                try
                {
                    request.Headers.TryAddWithoutValidation(headerName, values.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, $"Failed to copy header: {headerName}");
                }
            }
        }
    }

    private async Task CopyRequestBodyToRequest(HttpRequestMessage request)
    {
        if (HttpContext.Request.ContentLength > 0)
        {
            // 对于Git协议，需要正确处理流
            var contentType = HttpContext.Request.ContentType ?? "application/x-git-upload-pack-request";
            
            // 使用流来避免大文件的内存问题
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

    private async Task SetProxyResponse(HttpResponseMessage response)
    {
        // 设置状态码
        HttpContext.Response.StatusCode = (int)response.StatusCode;

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

        // 流式复制响应体
        await response.Content.CopyToAsync(HttpContext.Response.Body);
    }

    private static bool ShouldCopyResponseHeader(string headerName)
    {
        var skipHeaders = new[]
        {
            "transfer-encoding", "connection", "upgrade", "server"
        };
        return !skipHeaders.Contains(headerName.ToLowerInvariant());
    }
}
