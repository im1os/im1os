using iM1os.Application.Common;
using iM1os.Application.Tenancy;

namespace iM1os.Infrastructure.Services;

public sealed class TenantProvider(ICurrentUser currentUser) : ITenantProvider
{
    public Guid? CurrentOrganizationId => currentUser.OrganizationId;
}
