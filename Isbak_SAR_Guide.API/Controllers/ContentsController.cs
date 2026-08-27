using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Contents;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/modules/{moduleId:int}/contents")]
public class ContentsController(IContentService contentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int moduleId, [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] bool? isPublished, CancellationToken cancellationToken)
    {
        var result = await contentService.GetPagedAsync(moduleId, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, isPublished, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int moduleId, int id, CancellationToken cancellationToken)
    {
        var result = await contentService.GetByIdAsync(moduleId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int moduleId, CreateContentDto dto, CancellationToken cancellationToken)
    {
        var result = await contentService.CreateAsync(moduleId, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int moduleId, int id, UpdateContentDto dto, CancellationToken cancellationToken)
    {
        var result = await contentService.UpdateAsync(moduleId, id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int moduleId, int id, CancellationToken cancellationToken)
    {
        var result = await contentService.DeleteAsync(moduleId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(int moduleId, ReorderDto dto, CancellationToken cancellationToken)
    {
        var result = await contentService.ReorderAsync(moduleId, dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
