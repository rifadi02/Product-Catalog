namespace Catalog.Domain.Exceptions;

/// <summary>
/// A resource addressed by the request does not exist, or is soft-deleted and therefore
/// invisible behind the global query filter. Maps to 404 <c>resource_not_found</c>.
/// </summary>
public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} with id '{key}' was not found.")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
