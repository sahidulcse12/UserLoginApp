using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CategoryManagement")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService) => _categoryService = categoryService;

    // Accessible to any authenticated user for category dropdowns in product entry
    [HttpGet("lookup")]
    [Authorize]
    public async Task<IActionResult> Lookup()
        => Ok(await _categoryService.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search = null)
        => Ok(await _categoryService.GetAllAsync(search));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cat = await _categoryService.GetByIdAsync(id);
        return cat == null ? NotFound() : Ok(cat);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request)
    {
        var cat = await _categoryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = cat.Id }, cat);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest request)
    {
        var cat = await _categoryService.UpdateAsync(id, request);
        return cat == null ? NotFound() : Ok(cat);
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _categoryService.SetActiveAsync(id, true);
        return result ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _categoryService.SetActiveAsync(id, false);
        return result ? NoContent() : NotFound();
    }
}
