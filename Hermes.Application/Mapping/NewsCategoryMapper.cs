using System.Collections.Frozen;
using Hermes.Domain.Enums;

namespace Hermes.Application.Mapping;

/// <summary>
/// High-performance bidirectional mapper between <see cref="NewsCategory"/> enum values and external API category strings.
/// Uses immutable <see cref="FrozenDictionary{TKey, TValue}"/> for zero-allocation, O(1) hash lookups.
/// </summary>
public static class NewsCategoryMapper
{
    private static readonly FrozenDictionary<NewsCategory, string> _toApi = BuildForward();
    private static readonly FrozenDictionary<string, NewsCategory> _fromApi = BuildReverse();

    /// <summary>
    /// Converts a <see cref="NewsCategory"/> enum value to its corresponding lowercase API string representation.
    /// Eliminates runtime string allocations by returning pre-allocated frozen string references.
    /// </summary>
    /// <param name="category">The news category enum value.</param>
    /// <returns>The lowercase category string (e.g. "technology", "sports").</returns>
    public static string ToApiString(NewsCategory category)
    {
        if (!_toApi.TryGetValue(category, out string? value))
            return category.ToString().ToLowerInvariant();

        return value;
    }

    /// <summary>
    /// Attempts to parse an external category string into a <see cref="NewsCategory"/> enum value without memory allocation.
    /// Case-insensitive matching via FrozenDictionary.
    /// </summary>
    /// <param name="categoryString">The category string to parse.</param>
    /// <param name="category">When this method returns, contains the matching <see cref="NewsCategory"/> enum value.</param>
    /// <returns>True if the category was recognized; otherwise false.</returns>
    public static bool TryGetCategory(string? categoryString, out NewsCategory category)
    {
        category = default;
        if (string.IsNullOrWhiteSpace(categoryString))
            return false;

        return _fromApi.TryGetValue(categoryString.Trim(), out category);
    }

    /// <summary>
    /// Parses an external category string into a <see cref="NewsCategory"/> enum value or throws an exception.
    /// </summary>
    /// <param name="categoryString">The category string to parse.</param>
    /// <returns>The matching <see cref="NewsCategory"/> enum value.</returns>
    public static NewsCategory ParseCategory(string categoryString)
    {
        if (TryGetCategory(categoryString, out NewsCategory category))
            return category;

        throw new ArgumentException($"Unknown news category: {categoryString}", nameof(categoryString));
    }

    /// <summary>
    /// Pre-builds the forward mapping from enum values to lowercase API strings.
    /// </summary>
    /// <returns>A frozen dictionary mapping NewsCategory to lowercase string.</returns>
    private static FrozenDictionary<NewsCategory, string> BuildForward()
    {
        Dictionary<NewsCategory, string> map = [];
        foreach (NewsCategory value in Enum.GetValues<NewsCategory>())
        {
            map[value] = value.ToString().ToLowerInvariant();
        }

        return map.ToFrozenDictionary();
    }

    /// <summary>
    /// Pre-builds the reverse mapping from category strings to enum values with case-insensitive comparisons.
    /// </summary>
    /// <returns>A frozen dictionary mapping string to NewsCategory.</returns>
    private static FrozenDictionary<string, NewsCategory> BuildReverse()
    {
        Dictionary<string, NewsCategory> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<NewsCategory, string> kv in _toApi)
        {
            map[kv.Value] = kv.Key;
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
