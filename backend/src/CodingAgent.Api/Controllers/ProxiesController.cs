using CodingAgent.Api.Contracts.Proxies;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/proxies")]
public class ProxiesController(IProxyRepository proxies, ProxyHttpClientFactory proxyFactory) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProxyDto>>> GetAll(CancellationToken ct)
    {
        var list = await proxies.GetAllAsync(ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProxyDto>> Get(int id, CancellationToken ct)
    {
        var proxy = await proxies.GetAsync(id, ct);
        return proxy is null ? NotFound() : Ok(ToDto(proxy));
    }

    [HttpPost]
    public async Task<ActionResult<ProxyDto>> Create([FromBody] CreateProxyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Host) || request.Port <= 0)
        {
            return BadRequest(new { message = "name、host、port 必填且 port > 0" });
        }
        var proxy = new ProxyServer
        {
            Name = request.Name,
            Scheme = ParseScheme(request.Scheme),
            Host = request.Host,
            Port = request.Port,
            Username = request.Username,
            Password = request.Password,
            Enabled = request.Enabled,
        };
        await proxies.AddAsync(proxy, ct);
        return Created($"/api/proxies/{proxy.Id}", ToDto(proxy));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProxyRequest request, CancellationToken ct)
    {
        var proxy = await proxies.GetAsync(id, ct);
        if (proxy is null) return NotFound();
        proxy.Name = request.Name;
        proxy.Scheme = ParseScheme(request.Scheme);
        proxy.Host = request.Host;
        proxy.Port = request.Port;
        proxy.Username = request.Username;
        proxy.Password = request.Password;
        proxy.Enabled = request.Enabled;
        await proxies.UpdateAsync(proxy, ct);
        return Ok(ToDto(proxy));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await proxies.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<ProxyTestResponse>> Test(int id, CancellationToken ct)
    {
        var proxy = await proxies.GetAsync(id, ct);
        if (proxy is null) return NotFound();

        try
        {
            using var client = proxyFactory.CreateClient(proxy);
            client.Timeout = TimeSpan.FromSeconds(15);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.gstatic.com/generate_204");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var response = await client.SendAsync(request, ct);
            sw.Stop();
            return Ok(new ProxyTestResponse(
                true,
                $"连通成功（HTTP {(int)response.StatusCode}）",
                (int)sw.ElapsedMilliseconds));
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return Ok(new ProxyTestResponse(false, $"连通失败: {ex.Message}", null));
        }
    }

    private static ProxyScheme ParseScheme(string scheme) =>
        scheme.Contains("socks", StringComparison.OrdinalIgnoreCase) ? ProxyScheme.Socks5 : ProxyScheme.Http;

    internal static ProxyDto ToDto(ProxyServer proxy) => new(
        proxy.Id,
        proxy.Name,
        proxy.Scheme == ProxyScheme.Socks5 ? "socks5" : "http",
        proxy.Host,
        proxy.Port,
        proxy.Username,
        proxy.Password,
        proxy.Enabled);
}
