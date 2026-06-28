namespace iM1os.Application.CompanyUsers;

public interface ICompanyUserService
{
    Task<CompanyUsersWorkspace> GetWorkspaceAsync(Guid organizationId, Guid actorUserId, CompanyUserSearchRequest request, CancellationToken cancellationToken);

    Task<Guid> CreateUserAsync(Guid organizationId, Guid actorUserId, CreateCompanyUserRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task UpdateUserAsync(Guid organizationId, Guid actorUserId, UpdateCompanyUserRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task SavePermissionOverridesAsync(Guid organizationId, Guid actorUserId, SaveCompanyUserPermissionOverridesRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task RunSecurityActionAsync(Guid organizationId, Guid actorUserId, CompanyUserSecurityActionRequest request, string? ipAddress, CancellationToken cancellationToken);
}
