using AramaKurtarma.API.Extensions;
using AramaKurtarma.Business.Books;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace AramaKurtarma.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class BooksController(IBookService bookService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await bookService.GetAllAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await bookService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateBookDto dto, CancellationToken cancellationToken)
    {
        var result = await bookService.CreateAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateBookDto dto, CancellationToken cancellationToken)
    {
        var result = await bookService.UpdateAsync(id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await bookService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
