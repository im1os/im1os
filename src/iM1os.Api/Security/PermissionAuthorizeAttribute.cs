using Microsoft.AspNetCore.Authorization;

namespace iM1os.Api.Security;

public sealed class PermissionAuthorizeAttribute(string permission) : AuthorizeAttribute
{
    public string Permission { get; } = permission;
}
