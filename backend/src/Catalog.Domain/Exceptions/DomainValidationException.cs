namespace Catalog.Domain.Exceptions;

/// <summary>
/// An invariant guarded by an entity factory or behaviour method was violated. Translated to a
/// 400 <c>validation_failed</c> ProblemDetails by <c>ExceptionHandlingMiddleware</c> (§3.0.1).
///
/// This is a genuinely exceptional condition: request DTOs are validated before a handler runs,
/// so reaching a domain guard means a caller inside the process constructed an invalid entity.
/// It is deliberately an exception and not a <c>Result</c> — the domain is not in the business
/// of returning half-built objects.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
    public DomainValidationException(string message, Exception inner) : base(message, inner) { }
}
