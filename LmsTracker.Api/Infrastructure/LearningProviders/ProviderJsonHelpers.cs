using System.Text.Json;

namespace LmsTracker.Api.Infrastructure.LearningProviders;

/// <summary>
/// Best-effort JSON field lookups shared by learning-provider clients, since each
/// provider's response shape varies by account/tenant and isn't guaranteed by contract.
/// </summary>
public static class ProviderJsonHelpers
{
    public static string? GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    public static double? GetDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    public static JsonElement[]? GetArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return value.EnumerateArray().ToArray();
    }

    public static JsonElement? GetObject(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return value;
    }
}
