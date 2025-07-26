using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace DH.GitProxyCloner.Controllers;

[ApiController]
[Route("{*catchAll}")]
public class GitProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitProxyController> _logger;

    public GitProxyController(HttpClient httpClient, ILogger<GitProxyController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> ProxyGitRequest()
    {
        var requestPath = HttpContext.Request.Path.Value;
        var queryString = HttpContext.Request.QueryString.Value;
        
        // 提取GitHub URL
        var githubUrl = ExtractGithubUrl(requestPath);
        if (string.IsNullOrEmpty(githubUrl))
        {
            return BadRequest("Invalid GitHub URL format");
        }

        try
        {
            // 构建目标URL
            var targetUrl = $"{githubUrl}{queryString}";
            _logger.LogInformation($"Proxying request to: {targetUrl}");

            // 创建代理请求
            using var request = new HttpRequestMessage(
                new HttpMethod(HttpContext.Request.Method), 
                targetUrl);

            // 复制请求头（排除一些不需要的头）
            foreach (var header in HttpContext.Request.Headers)
            {
                if (IsAllowedHeader(header.Key))
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            // 如果是POST请求，复制请求体
            if (HttpContext.Request.Method == "POST")
            {
                var requestBody = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(requestBody))
                {
                    request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, 
                        HttpContext.Request.ContentType ?? "application/x-git-upload-pack-request");
                }
            }

            // 发送请求到GitHub
            using var response = await _httpClient.SendAsync(request);

            // 复制响应头
            foreach (var header in response.Headers)
            {
                HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }

            foreach (var header in response.Content.Headers)
            {
                HttpContext.Response.Headers.TryAdd(header.Key, header.Value.ToArray());
            }

            // 设置响应状态码
            HttpContext.Response.StatusCode = (int)response.StatusCode;

            // 复制响应体
            await response.Content.CopyToAsync(HttpContext.Response.Body);

            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error proxying request to {githubUrl}");
            return StatusCode(500, "Proxy error");
        }
    }

    private string ExtractGithubUrl(string requestPath)
    {
        // 支持多种URL格式
        // 格式1: /https://github.com/user/repo.git/...
        // 格式2: /user/repo/...
        
        if (string.IsNullOrEmpty(requestPath))
            return string.Empty;

        // 移除开头的斜杠
        requestPath = requestPath.TrimStart('/');

        // 如果直接包含完整的GitHub URL
        if (requestPath.StartsWith("https://github.com/"))
        {
            var match = Regex.Match(requestPath, @"^(https://github\.com/[^/]+/[^/]+(?:\.git)?)(.*)$");
            if (match.Success)
            {
                return match.Groups[1].Value + match.Groups[2].Value;
            }
        }

        // 如果是简化格式 user/repo
        var simpleMatch = Regex.Match(requestPath, @"^([^/]+/[^/]+)(.*)$");
        if (simpleMatch.Success)
        {
            return $"https://github.com/{simpleMatch.Groups[1].Value}.git{simpleMatch.Groups[2].Value}";
        }

        return string.Empty;
    }

    private static bool IsAllowedHeader(string headerName)
    {
        var disallowedHeaders = new[]
        {
            "host", "connection", "transfer-encoding", "content-length"
        };

        return !disallowedHeaders.Contains(headerName.ToLowerInvariant());
    }
}
