using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RoleManagement")]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _roleService.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await _roleService.GetByIdAsync(id);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpGet("features")]
    public async Task<IActionResult> GetAllFeatures() => Ok(await _roleService.GetAllFeaturesAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var role = await _roleService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _roleService.UpdateAsync(id, request);
        return role == null ? NotFound() : Ok(role);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _roleService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/features")]
    public async Task<IActionResult> AssignFeature(Guid id, [FromBody] AssignFeatureRequest request)
    {
        var result = await _roleService.AssignFeatureAsync(id, request.FeatureId);
        return result ? NoContent() : BadRequest();
    }

    [HttpDelete("{id:guid}/features/{featureId:guid}")]
    public async Task<IActionResult> RemoveFeature(Guid id, Guid featureId)
    {
        var result = await _roleService.RemoveFeatureAsync(id, featureId);
        return result ? NoContent() : NotFound();
    }
}
