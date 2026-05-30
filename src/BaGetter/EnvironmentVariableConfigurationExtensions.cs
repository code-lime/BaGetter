using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace BaGetter;

public static class EnvironmentVariableConfigurationExtensions
{
    private static readonly Regex PlaceholderPattern = new(
        @"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IConfigurationBuilder AddEnvironmentVariablePlaceholders(
        this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configuration = builder.Build();
        var expandedValues = new Dictionary<string, string>();

        foreach (var pair in configuration.AsEnumerable())
        {
            if (pair.Value == null)
            {
                continue;
            }

            var expandedValue = ExpandPlaceholders(pair.Value);
            if (!string.Equals(pair.Value, expandedValue, StringComparison.Ordinal))
            {
                expandedValues[pair.Key] = expandedValue;
            }
        }

        return expandedValues.Count == 0
            ? builder
            : builder.AddInMemoryCollection(expandedValues);
    }

    private static string ExpandPlaceholders(string value)
    {
        return PlaceholderPattern.Replace(
            value,
            match => Environment.GetEnvironmentVariable(match.Groups["name"].Value) ?? match.Value);
    }
}
