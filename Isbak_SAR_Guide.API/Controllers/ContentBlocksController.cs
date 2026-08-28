using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.ContentBlocks;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/contents/{contentId:int}/blocks")]
public class ContentBlocksController(IContentBlockService contentBlockService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int contentId, [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.GetPagedAsync(contentId, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int contentId, int id, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.GetByIdAsync(contentId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int contentId, CreateContentBlockDto dto, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.CreateAsync(contentId, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int contentId, int id, UpdateContentBlockDto dto, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.UpdateAsync(contentId, id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int contentId, int id, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.DeleteAsync(contentId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(int contentId, ReorderDto dto, CancellationToken cancellationToken)
    {
        var result = await contentBlockService.ReorderAsync(contentId, dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
