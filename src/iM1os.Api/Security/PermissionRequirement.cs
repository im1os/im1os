using Microsoft.AspNetCore.Authorization;

namespace iM1os.Api.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
