namespace Catalog.Api.Logging;

public sealed class RedactingDestructuringPolicy : IDestructuringPolicy
{
    private const string Mask = "***REDACTED***";

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "confirmPassword", "currentPassword", "newPassword",
        "accessToken", "refreshToken", "token", "tokenHash",
        "passwordHash", "signingKey", "authorization", "apiKey", "secret"
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory factory,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        var type = value.GetType();

        if (type.IsPrimitive || type == typeof(string) || type.Namespace?.StartsWith("System", StringComparison.Ordinal) is true)
        {
            result = null;
            return false;
        }

        var properties = type.GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        if (!properties.Any(p => SensitiveNames.Contains(p.Name)))
        {
            result = null;
            return false;
        }

        var members = new List<LogEventProperty>(properties.Length);

        foreach (var property in properties)
        {
            if (SensitiveNames.Contains(property.Name))
            {
                members.Add(new LogEventProperty(property.Name, new ScalarValue(Mask)));
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (Exception ex)
            {
                members.Add(new LogEventProperty(property.Name, new ScalarValue($"<{ex.GetType().Name}>")));
                continue;
            }

            members.Add(new LogEventProperty(property.Name, factory.CreatePropertyValue(propertyValue, true)));
        }

        result = new StructureValue(members, type.Name);
        return true;
    }
}
