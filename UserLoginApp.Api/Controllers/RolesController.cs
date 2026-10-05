using Microsoft.AspNetCore.Mvc;

namespace UserLoginApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Role-based features will be implemented in Phase 2." });
    }
}
