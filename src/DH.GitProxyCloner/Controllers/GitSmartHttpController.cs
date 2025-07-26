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
            return Ok("Git Proxy Cloner is running. Usage: clone with your-domain.com/user/repo or your-domain.com/https://github.com/user/repo.git");
        }

        // 尝试解析GitHub URL
        var githubUrl = ParseGithubUrl(requestPath);
        if (string.IsNullOrEmpty(githubUrl))
        {
            return BadRequest("Invalid GitHub repository URL format");
        }

        var fullTargetUrl = $"{githubUrl}{queryString}";
        _logger.LogInformation($"Universal proxy to: {fullTargetUrl}");

        return await ProxyToGithub(fullTargetUrl);
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
            
            // 如果没有.git后缀，自动添加
            if (!userRepo.EndsWith(".git"))
            {
                userRepo += ".git";
            }
            
            return $"https://github.com/{userRepo}{path}";
        }

        return string.Empty;
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
