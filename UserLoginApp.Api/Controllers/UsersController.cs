using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserLoginApp.Api.Models.DTOs;
using UserLoginApp.Api.Services;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _userService.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var user = await _userService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userService.UpdateAsync(id, request);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var result = await _userService.SetActiveAsync(id, true);
        return result ? NoContent() : NotFound();
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var result = await _userService.SetActiveAsync(id, false);
        return result ? NoContent() : NotFound();
    }
}
