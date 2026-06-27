using System.Security.Claims;
using iM1os.Application.Common;

namespace iM1os.Api.Security;

public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");

    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");

    public Guid? OrganizationId
    {
        get
        {
            var value = User?.FindFirstValue("organization_id");
            return Guid.TryParse(value, out var organizationId) ? organizationId : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}
