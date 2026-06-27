using iM1os.Application.Platform;

namespace iM1os.Infrastructure.Services;

public sealed class NoOpWelcomeEmailSender : IWelcomeEmailSender
{
    public Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
