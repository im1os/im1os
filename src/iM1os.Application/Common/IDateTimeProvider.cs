namespace iM1os.Application.Common;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
