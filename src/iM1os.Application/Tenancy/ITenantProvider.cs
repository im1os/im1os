namespace iM1os.Application.Tenancy;

public interface ITenantProvider
{
    Guid? CurrentOrganizationId { get; }
}
