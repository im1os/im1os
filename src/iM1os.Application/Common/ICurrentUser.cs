namespace iM1os.Application.Common;

public interface ICurrentUser
{
    string? UserId { get; }

    string? Email { get; }

    Guid? OrganizationId { get; }

    bool IsAuthenticated { get; }
}
