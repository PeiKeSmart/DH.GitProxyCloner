using DH.GitProxyCloner.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 配置HTTP客户端
builder.Services.AddHttpClient<GitSmartHttpController>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10); // Git操作可能需要较长时间
    client.DefaultRequestHeaders.Add("User-Agent", "git/2.0.0 (GitProxyCloner)");
});

builder.Services.AddHttpClient<UniversalGitProxyController>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
    client.DefaultRequestHeaders.Add("User-Agent", "git/2.0.0 (GitProxyCloner)");
});

// 配置 GitHub Web 代理的 HTTP 客户端
builder.Services.AddHttpClient<GitHubWebProxyController>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36 GitHubWebProxy/1.0");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 移除HTTPS重定向，因为可能需要支持HTTP的git请求
// app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
