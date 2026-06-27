namespace iM1os.Application.Platform;

public interface IWelcomeEmailSender
{
    Task SendAsync(string recipientEmail, string subject, string body, CancellationToken cancellationToken);
}
