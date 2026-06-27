namespace iM1os.Application.Authentication;

public sealed record LoginRequest(string Email, string Password, Guid? OrganizationId);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserProfile User);

public sealed record UserProfile(Guid Id, Guid OrganizationId, string Email, string DisplayName, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);
