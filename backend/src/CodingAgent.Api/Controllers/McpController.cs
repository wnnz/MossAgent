using System.Text.Json;
using CodingAgent.Api.Contracts.Mcp;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Mcp;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpController(IMcpRepository mcpRepository, McpConnectionRegistry registry) : ControllerBase
{
    [HttpGet("servers")]
    public async Task<ActionResult<List<McpServerDto>>> GetServers(CancellationToken ct)
    {
        var list = await mcpRepository.GetAllServersAsync(ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpPost("servers")]
    public async Task<ActionResult<McpServerDto>> CreateServer([FromBody] CreateMcpServerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "name 不能为空" });
        }
        var transport = ParseTransport(request.Transport);
        if (transport == McpTransport.Stdio && string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest(new { message = "stdio 传输必须提供 command" });
        }
        if (transport == McpTransport.Http && string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { message = "http 传输必须提供 url" });
        }

        var server = new McpServer
        {
            Name = request.Name,
            Transport = transport,
            Command = request.Command,
            ArgsJson = request.ArgsJson,
            EnvJson = request.EnvJson,
            Url = request.Url,
            HeadersJson = request.HeadersJson,
            Enabled = request.Enabled,
            AutoConnect = request.AutoConnect,
        };
        await mcpRepository.AddServerAsync(server, ct);
        return Created($"/api/mcp/servers/{server.Id}", ToDto(server));
    }

    [HttpPut("servers/{id:int}")]
    public async Task<IActionResult> UpdateServer(int id, [FromBody] UpdateMcpServerRequest request, CancellationToken ct)
    {
        var server = await mcpRepository.GetServerAsync(id, ct);
        if (server is null) return NotFound();
        // 配置变更后断开旧连接
        await registry.DisconnectAsync(id);
        server.Name = request.Name;
        server.Transport = ParseTransport(request.Transport);
        server.Command = request.Command;
        server.ArgsJson = request.ArgsJson;
        server.EnvJson = request.EnvJson;
        server.Url = request.Url;
        server.HeadersJson = request.HeadersJson;
        server.Enabled = request.Enabled;
        server.AutoConnect = request.AutoConnect;
        await mcpRepository.UpdateServerAsync(server, ct);
        return Ok(ToDto(server));
    }

    [HttpDelete("servers/{id:int}")]
    public async Task<IActionResult> DeleteServer(int id, CancellationToken ct)
    {
        await registry.DisconnectAsync(id);
        await mcpRepository.DeleteServerAsync(id, ct);
        return NoContent();
    }

    [HttpPost("servers/{id:int}/connect")]
    public async Task<ActionResult<List<McpToolDto>>> Connect(int id, CancellationToken ct)
    {
        var server = await mcpRepository.GetServerAsync(id, ct);
        if (server is null) return NotFound();

        try
        {
            var connection = registry.GetOrConnect(server, ct);
            var tools = await connection.ListToolsAsync(ct);
            await mcpRepository.ReplaceToolsAsync(id, tools.Select(t => new McpTool
            {
                ServerId = id,
                Name = t.Name,
                Description = t.Description,
                SchemaJson = t.SchemaJson,
            }), ct);
            var cached = await mcpRepository.GetToolsAsync(id, ct);
            return Ok(cached.Select(ToToolDto).ToList());
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or JsonException)
        {
            await registry.DisconnectAsync(id);
            return BadRequest(new { message = $"连接失败: {ex.Message}" });
        }
    }

    [HttpPost("servers/{id:int}/disconnect")]
    public async Task<IActionResult> Disconnect(int id, CancellationToken ct)
    {
        await registry.DisconnectAsync(id);
        return Ok(new { message = "已断开" });
    }

    [HttpGet("servers/{id:int}/tools")]
    public async Task<ActionResult<List<McpToolDto>>> GetTools(int id, CancellationToken ct)
    {
        if (await mcpRepository.GetServerAsync(id, ct) is null) return NotFound();
        var tools = await mcpRepository.GetToolsAsync(id, ct);
        return Ok(tools.Select(ToToolDto).ToList());
    }

    [HttpPost("servers/{id:int}/tools/{toolName}/call")]
    public async Task<ActionResult<McpToolCallResponse>> CallTool(int id, string toolName, [FromBody] McpToolCallRequest request, CancellationToken ct)
    {
        var server = await mcpRepository.GetServerAsync(id, ct);
        if (server is null) return NotFound();

        var connection = registry.Get(id);
        if (connection is null || !connection.IsConnected)
        {
            return BadRequest(new { message = "服务器未连接，请先连接" });
        }

        var argsJson = request.Arguments is null
            ? "{}"
            : JsonSerializer.Serialize(request.Arguments);
        try
        {
            var result = await connection.CallToolAsync(toolName, argsJson, ct);
            return Ok(new McpToolCallResponse(true, result));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or JsonException)
        {
            return Ok(new McpToolCallResponse(false, ex.Message));
        }
    }

    private static McpTransport ParseTransport(string transport) =>
        transport.Contains("http", StringComparison.OrdinalIgnoreCase) ? McpTransport.Http : McpTransport.Stdio;

    internal static McpServerDto ToDto(McpServer s) => new(
        s.Id,
        s.Name,
        s.Transport == McpTransport.Http ? "http" : "stdio",
        s.Command,
        s.ArgsJson,
        s.EnvJson,
        s.Url,
        s.HeadersJson,
        s.Enabled,
        s.AutoConnect);

    internal static McpToolDto ToToolDto(McpTool t) => new(t.Id, t.ServerId, t.Name, t.Description, t.SchemaJson);
}
