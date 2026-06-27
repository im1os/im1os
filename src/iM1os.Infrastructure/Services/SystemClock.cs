using iM1os.Application.Common;

namespace iM1os.Infrastructure.Services;

public sealed class SystemClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
