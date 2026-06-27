using iM1os.Application.Common;

namespace iM1os.Infrastructure.Services;

public sealed class NoCurrentUser : ICurrentUser
{
    public string? UserId => null;

    public string? Email => null;

    public Guid? OrganizationId => null;

    public bool IsAuthenticated => false;
}
