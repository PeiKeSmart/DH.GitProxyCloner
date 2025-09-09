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
