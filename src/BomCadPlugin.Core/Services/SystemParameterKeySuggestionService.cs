namespace BomCadPlugin.Core.Services;

public static class SystemParameterKeySuggestionService
{
    public static bool IsKeyInUse(IEnumerable<string> existingKeys, string key)
    {
        var normalizedKey = key.Trim();
        return existingKeys.Any(existingKey => string.Equals(existingKey.Trim(), normalizedKey, StringComparison.OrdinalIgnoreCase));
    }

    public static string SuggestAvailableKey(IEnumerable<string> existingKeys, string requestedKey)
    {
        var key = requestedKey.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            return "";
        }

        var usedKeys = existingKeys
            .Where(existingKey => !string.IsNullOrWhiteSpace(existingKey))
            .Select(existingKey => existingKey.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseKey = TrimTrailingDigits(key);
        var index = 1;
        var candidate = $"{baseKey}{index}";

        while (usedKeys.Contains(candidate))
        {
            index++;
            candidate = $"{baseKey}{index}";
        }

        return candidate;
    }

    private static string TrimTrailingDigits(string key)
    {
        var index = key.Length;
        while (index > 0 && char.IsDigit(key[index - 1]))
        {
            index--;
        }

        return index == 0 ? key : key[..index];
    }
}
