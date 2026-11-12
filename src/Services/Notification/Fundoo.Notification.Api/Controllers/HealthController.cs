using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notification.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new { status = "Healthy", service = "Notification" });
    }
}
