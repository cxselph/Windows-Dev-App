using System.IO;
using System.Text;
using WindowsDevApp.Models;

namespace WindowsDevApp.Services;

/// <summary>
/// Reads and rewrites simple KEY=VALUE .env files, preserving comments,
/// blank lines, and the original ordering of every line it doesn't touch.
/// </summary>
public static class EnvFileService
{
    public static List<EnvField> Load(string path)
    {
        var fields = new List<EnvField>();
        if (!File.Exists(path)) return fields;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.TrimStart();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            fields.Add(new EnvField { Key = key, Value = Unquote(value) });
        }

        return fields;
    }

    /// <summary>Replaces the value for <paramref name="key"/>, or appends a new line if it isn't present.</summary>
    public static void SetField(string path, string key, string newValue)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        var encoded = $"{key}={Quote(newValue)}";
        var found = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith('#')) continue;

            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;

            var lineKey = trimmed[..eq].Trim();
            if (string.Equals(lineKey, key, StringComparison.Ordinal))
            {
                lines[i] = encoded;
                found = true;
                break;
            }
        }

        if (!found) lines.Add(encoded);

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1];
        }
        return value;
    }

    private static string Quote(string value)
    {
        var needsQuotes = value.Length == 0 || value.Contains(' ') || value.Contains('#') ||
                           value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes) return value;

        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r\n", "\n")
            .Replace("\n", "\\n");
        return $"\"{escaped}\"";
    }
}
