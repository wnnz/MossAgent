using CodingAgent.Agent;
using CodingAgent.Api.Middleware;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using CodingAgent.Infrastructure.Http;
using CodingAgent.Infrastructure.Llm;
using CodingAgent.Infrastructure.Mcp;
using CodingAgent.Infrastructure.Repositories;
using CodingAgent.Infrastructure.Security;
using CodingAgent.Infrastructure.Storage;
using CodingAgent.Infrastructure.Tools;
using CodingAgent.Api.Sse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ---- 数据目录：默认 backend/data，可用 CODINGAGENT_DATA_DIR 覆盖 ----
var contentRoot = builder.Environment.ContentRootPath;
string backendRoot;
try
{
    // backend/src/CodingAgent.Api → backend/src → backend
    var up1 = Directory.GetParent(contentRoot)?.FullName;
    var up2 = up1 is null ? null : Directory.GetParent(up1)?.FullName;
    backendRoot = up2 is not null && Path.GetFileName(up2) == "backend" ? up2 : contentRoot;
}
catch (Exception)
{
    backendRoot = contentRoot;
}
var repoRoot = backendRoot == contentRoot ? contentRoot : Directory.GetParent(backendRoot)!.FullName;
var dataDir = Environment.GetEnvironmentVariable("CODINGAGENT_DATA_DIR")
    ?? Path.Combine(backendRoot, "data");
Directory.CreateDirectory(dataDir);
var defaultWorkspaceRoot = Path.Combine(dataDir, "workspace");
Directory.CreateDirectory(defaultWorkspaceRoot);

// ---- 首次启动建库 + 种子 + 读取 JWT 密钥 ----
var dbPath = Path.Combine(dataDir, "codingagent.db");
var bootstrapOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
using (var bootstrapDb = new AppDbContext(bootstrapOptions))
{
    await DbSeeder.SeedAsync(bootstrapDb);
}
string jwtSecret;
using (var bootstrapDb = new AppDbContext(bootstrapOptions))
{
    jwtSecret = await bootstrapDb.Settings
        .Where(s => s.Key == DbSeeder.JwtSecretKey)
        .Select(s => s.Value)
        .FirstAsync();
}

// ---- JSON 契约：camelCase + 枚举 lower_snake_case ----
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new LowerSnakeCaseEnumConverterFactory());
});

// ---- EF Core ----
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

// ---- 仓储 ----
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IProviderRepository, ProviderRepository>();
builder.Services.AddScoped<IProxyRepository, ProxyRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISubAgentRepository, SubAgentRepository>();
builder.Services.AddScoped<IMemoryRepository, MemoryRepository>();
builder.Services.AddScoped<ISkillRepository, SkillRepository>();
builder.Services.AddScoped<IMcpRepository, McpRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();

// ---- 安全 ----
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService>(new JwtCookieTokenService(jwtSecret));

// ---- 存储与代理 ----
builder.Services.AddScoped<IWorkspaceLocator>(sp =>
    new WorkspaceLocator(sp.GetRequiredService<ISettingsRepository>(), defaultWorkspaceRoot));
builder.Services.AddSingleton<ProxyHttpClientFactory>();
builder.Services.AddSingleton<IModelCatalog, ModelCatalog>();

// ---- LLM ----
builder.Services.AddScoped<ILlmClientFactory, LlmClientFactory>();

// ---- MCP ----
builder.Services.AddSingleton<IMcpConnectionFactory, McpConnectionFactory>();
builder.Services.AddSingleton<McpConnectionRegistry>();
builder.Services.AddScoped<McpToolBridge>();

// ---- 工具与 Agent ----
builder.Services.AddScoped<ContextWindowManager>();
builder.Services.AddScoped<SystemPromptBuilder>();
builder.Services.AddScoped<AgentLoop>();
builder.Services.AddScoped<ISubAgentRunner, SubAgentRunner>();
builder.Services.AddScoped<ITool, ReadFileTool>();
builder.Services.AddScoped<ITool, WriteFileTool>();
builder.Services.AddScoped<ITool, EditFileTool>();
builder.Services.AddScoped<ITool, ListDirTool>();
builder.Services.AddScoped<ITool, GlobTool>();
builder.Services.AddScoped<ITool, GrepTool>();
builder.Services.AddScoped<ITool, RunCommandTool>();
builder.Services.AddScoped<ITool, MemorySaveTool>();
builder.Services.AddScoped<ITool, MemorySearchTool>();
builder.Services.AddScoped<ITool, SkillLoadTool>();
builder.Services.AddScoped<ITool, TaskTool>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddSingleton<SessionCancellationMap>();

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<CookieAuthMiddleware>();

// ---- 生产托管前端 dist（存在时），单端口 ----
var frontendDist = Path.Combine(repoRoot, "frontend", "dist");
if (Directory.Exists(frontendDist))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(frontendDist),
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendDist),
    });
}

app.MapControllers();

// SPA fallback（仅在前端产物存在时）
if (Directory.Exists(frontendDist))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendDist),
    });
}

app.Logger.LogInformation("CodingAgent 启动：数据目录 {DataDir}，默认工作区 {Workspace}", dataDir, defaultWorkspaceRoot);
app.Run();
