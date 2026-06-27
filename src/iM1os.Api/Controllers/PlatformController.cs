using Asp.Versioning;
using iM1os.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iM1os.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/platform")]
public sealed class PlatformController(ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(new
        {
            name = "iM1 OS",
            domain = "im1os.com",
            authenticated = currentUser.IsAuthenticated,
            currentUser.OrganizationId
        });
    }
}
