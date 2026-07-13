using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ProductManagement")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService) => _productService = productService;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _productService.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _productService.GetByIdAsync(id);
        return product == null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductRequest request)
    {
        var product = await _productService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ProductRequest request)
    {
        var product = await _productService.UpdateAsync(id, request);
        return product == null ? NotFound() : Ok(product);
    }
}
