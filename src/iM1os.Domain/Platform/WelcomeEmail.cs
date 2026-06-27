using iM1os.Domain.Common;

namespace iM1os.Domain.Platform;

public sealed class WelcomeEmail : Entity
{
    public Guid OrganizationId { get; set; }

    public required string RecipientEmail { get; set; }

    public required string RecipientName { get; set; }

    public required string Subject { get; set; }

    public required string Body { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? SentAtUtc { get; set; }
}
