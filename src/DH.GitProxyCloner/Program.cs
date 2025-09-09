using DH.GitProxyCloner.Controllers;

using NewLife.Log;

XTrace.UseConsole();

XTrace.WriteLine($"准备启动");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 添加 YARP 服务
builder.Services.AddReverseProxy();

// 配置HTTP客户端
builder.Services.AddHttpClient<GitSmartHttpController>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10); // Git操作可能需要较长时间
    client.DefaultRequestHeaders.Add("User-Agent", "git/2.0.0 (GitProxyCloner)");
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
