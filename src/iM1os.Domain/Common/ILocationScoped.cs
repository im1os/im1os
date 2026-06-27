namespace iM1os.Domain.Common;

public interface ILocationScoped
{
    Guid? LocationId { get; set; }
}
