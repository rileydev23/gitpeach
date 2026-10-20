namespace GitPeach.Services;

public static class FuzzyMatcher
{
    /// <summary>True when every character of <paramref name="query"/> appears in order in <paramref name="text"/> (case-insensitive).</summary>
    public static bool Matches(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        int i = 0;
        foreach (var ch in text)
        {
            if (char.ToLowerInvariant(ch) == char.ToLowerInvariant(query[i]) && ++i == query.Length) return true;
        }
        return false;
    }
}
