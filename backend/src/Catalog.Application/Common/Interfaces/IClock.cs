namespace Catalog.Application.Common.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}
