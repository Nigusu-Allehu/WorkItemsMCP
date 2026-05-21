using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using WorkItemsMcp.Services;
using WorkItemsMcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<VaultService>();
builder.Services.AddSingleton<TaskIdGenerator>();
builder.Services.AddSingleton<MarkdownTaskSerializer>();
builder.Services.AddSingleton<TaskRepository>();
builder.Services.AddSingleton<DailyNoteService>();
builder.Services.AddSingleton<ViewBuilder>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly);

await builder.Build().RunAsync();
