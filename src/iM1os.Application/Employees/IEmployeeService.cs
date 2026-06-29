namespace iM1os.Application.Employees;

public interface IEmployeeService
{
    Task<EmployeesWorkspace> GetWorkspaceAsync(Guid organizationId, Guid actorUserId, EmployeeSearchRequest request, CancellationToken cancellationToken);

    Task<Guid> CreateEmployeeAsync(Guid organizationId, Guid actorUserId, CreateEmployeeRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task UpdateEmployeeAsync(Guid organizationId, Guid actorUserId, UpdateEmployeeRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task EnableLoginAccountAsync(Guid organizationId, Guid actorUserId, EnableEmployeeLoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task SaveCompensationAsync(Guid organizationId, Guid actorUserId, SaveEmployeeCompensationRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task DeleteCompensationAsync(Guid organizationId, Guid actorUserId, DeleteEmployeeCompensationRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task AddDocumentAsync(Guid organizationId, Guid actorUserId, AddEmployeeDocumentRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task SavePinAsync(Guid organizationId, Guid actorUserId, SaveEmployeePinRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task SavePermissionOverridesAsync(Guid organizationId, Guid actorUserId, SaveEmployeePermissionOverridesRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task RunSecurityActionAsync(Guid organizationId, Guid actorUserId, EmployeeSecurityActionRequest request, string? ipAddress, CancellationToken cancellationToken);
}
