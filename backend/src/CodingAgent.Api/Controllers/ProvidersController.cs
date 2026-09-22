using CodingAgent.Api.Contracts.Providers;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/providers")]
public class ProvidersController(IProviderRepository providers, IModelCatalog catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProviderDto>>> GetAll(CancellationToken ct)
    {
        var list = await providers.GetAllAsync(ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProviderDto>> Get(int id, CancellationToken ct)
    {
        var provider = await providers.GetAsync(id, ct);
        return provider is null ? NotFound() : Ok(ToDto(provider));
    }

    [HttpPost]
    public async Task<ActionResult<ProviderDto>> Create([FromBody] CreateProviderRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            return BadRequest(new { message = "name 和 baseUrl 不能为空" });
        }
        var provider = new Provider
        {
            Name = request.Name,
            Type = ParseType(request.Type),
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            ApiKey = request.ApiKey,
            ProxyId = request.ProxyId,
            Enabled = request.Enabled,
        };
        await providers.AddAsync(provider, ct);
        return Created($"/api/providers/{provider.Id}", ToDto(provider));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProviderRequest request, CancellationToken ct)
    {
        var provider = await providers.GetAsync(id, ct);
        if (provider is null) return NotFound();
        provider.Name = request.Name;
        provider.Type = ParseType(request.Type);
        provider.BaseUrl = request.BaseUrl.TrimEnd('/');
        provider.ApiKey = request.ApiKey;
        provider.ProxyId = request.ProxyId;
        provider.Enabled = request.Enabled;
        await providers.UpdateAsync(provider, ct);
        return Ok(ToDto(provider));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await providers.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---- 模型 ----

    [HttpPost("{id:int}/models/refresh")]
    public async Task<ActionResult<ModelRefreshResponse>> RefreshModels(int id, CancellationToken ct)
    {
        var provider = await providers.GetWithModelsAsync(id, ct);
        if (provider is null) return NotFound();

        List<string> remoteIds;
        try
        {
            remoteIds = await catalog.FetchModelIdsAsync(provider, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return BadRequest(new { message = $"拉取模型列表失败: {ex.Message}" });
        }

        var added = 0;
        var existingIds = provider.Models.Select(m => m.ModelId).ToHashSet();
        var remoteSet = remoteIds.ToHashSet();

        // 删除远端已不存在的非自定义模型
        var stale = provider.Models.Where(m => !m.IsCustom && !remoteSet.Contains(m.ModelId)).ToList();
        foreach (var staleModel in stale)
        {
            await providers.DeleteModelAsync(id, staleModel.Id, ct);
        }

        foreach (var modelId in remoteIds.Where(rid => !existingIds.Contains(rid)))
        {
            await providers.AddModelAsync(new ProviderModel
            {
                ProviderId = id,
                ModelId = modelId,
                DisplayName = modelId,
            }, ct);
            added++;
        }

        var updated = await providers.GetWithModelsAsync(id, ct);
        return Ok(new ModelRefreshResponse(added, -stale.Count, updated!.Models.Select(ToModelDto).ToList()));
    }

    [HttpPost("{id:int}/models")]
    public async Task<ActionResult<ProviderModelDto>> AddModel(int id, [FromBody] CreateModelRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId)) return BadRequest(new { message = "modelId 不能为空" });
        if (await providers.GetAsync(id, ct) is null) return NotFound();
        var model = new ProviderModel
        {
            ProviderId = id,
            ModelId = request.ModelId,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.ModelId : request.DisplayName,
            SupportsTools = request.SupportsTools,
            SupportsReasoning = request.SupportsReasoning,
            DefaultReasoningEffort = ParseEffort(request.DefaultReasoningEffort),
            MaxContextTokens = request.MaxContextTokens,
            MaxOutputTokens = request.MaxOutputTokens,
            IsCustom = true,
        };
        await providers.AddModelAsync(model, ct);
        return Created($"/api/providers/{id}/models/{model.Id}", ToModelDto(model));
    }

    [HttpPut("{id:int}/models/{modelId:int}")]
    public async Task<IActionResult> UpdateModel(int id, int modelId, [FromBody] UpdateModelRequest request, CancellationToken ct)
    {
        var model = await providers.GetModelAsync(id, modelId, ct);
        if (model is null) return NotFound();
        model.ModelId = request.ModelId;
        model.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? model.ModelId : request.DisplayName;
        model.SupportsTools = request.SupportsTools;
        model.SupportsReasoning = request.SupportsReasoning;
        model.DefaultReasoningEffort = ParseEffort(request.DefaultReasoningEffort);
        model.MaxContextTokens = request.MaxContextTokens;
        model.MaxOutputTokens = request.MaxOutputTokens;
        model.IsCustom = request.IsCustom;
        await providers.UpdateModelAsync(model, ct);
        return Ok(ToModelDto(model));
    }

    [HttpDelete("{id:int}/models/{modelId:int}")]
    public async Task<IActionResult> DeleteModel(int id, int modelId, CancellationToken ct)
    {
        await providers.DeleteModelAsync(id, modelId, ct);
        return NoContent();
    }

    private static ProviderType ParseType(string type) =>
        type.Contains("anthropic", StringComparison.OrdinalIgnoreCase) ? ProviderType.Anthropic : ProviderType.OpenAiCompatible;

    private static ReasoningEffort ParseEffort(string effort) =>
        Enum.TryParse<ReasoningEffort>(effort, true, out var parsed) ? parsed : ReasoningEffort.Off;

    internal static ProviderDto ToDto(Provider provider) => new(
        provider.Id,
        provider.Name,
        ToSnake(provider.Type),
        provider.BaseUrl,
        provider.ApiKey,
        provider.ProxyId,
        provider.Enabled,
        provider.Models.Select(ToModelDto).ToList());

    internal static ProviderModelDto ToModelDto(ProviderModel model) => new(
        model.Id,
        model.ProviderId,
        model.ModelId,
        model.DisplayName,
        model.SupportsTools,
        model.SupportsReasoning,
        ToSnake(model.DefaultReasoningEffort),
        model.MaxContextTokens,
        model.MaxOutputTokens,
        model.IsCustom);

    internal static string ToSnake(Enum value) => Sse.EnumSnakeCase.ToSnake(value);
}
