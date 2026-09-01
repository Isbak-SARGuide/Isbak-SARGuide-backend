using Asp.Versioning;
using Isbak_SAR_Guide.API.Common;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Common;
using Isbak_SAR_Guide.Business.DTOs.Modules;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/books/{bookId:int}/modules")]
public class ModulesController(IModuleService moduleService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        int bookId, [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] bool? isPublished, CancellationToken cancellationToken)
    {
        var result = await moduleService.GetPagedAsync(
            bookId, PagingDefaults.NormalizePage(page), PagingDefaults.NormalizePageSize(pageSize), isPublished, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int bookId, int id, CancellationToken cancellationToken)
    {
        var result = await moduleService.GetByIdAsync(bookId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int bookId, CreateModuleDto dto, CancellationToken cancellationToken)
    {
        var result = await moduleService.CreateAsync(bookId, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int bookId, int id, UpdateModuleDto dto, CancellationToken cancellationToken)
    {
        var result = await moduleService.UpdateAsync(bookId, id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int bookId, int id, CancellationToken cancellationToken)
    {
        var result = await moduleService.DeleteAsync(bookId, id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(int bookId, ReorderDto dto, CancellationToken cancellationToken)
    {
        var result = await moduleService.ReorderAsync(bookId, dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
