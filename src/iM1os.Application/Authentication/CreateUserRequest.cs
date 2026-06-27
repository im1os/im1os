namespace iM1os.Application.Authentication;

public sealed record CreateUserRequest(Guid OrganizationId, string Email, string DisplayName, string Password, IReadOnlyCollection<string> RoleNames);
