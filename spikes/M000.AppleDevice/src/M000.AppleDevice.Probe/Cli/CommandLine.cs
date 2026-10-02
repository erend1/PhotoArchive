using System.Globalization;

namespace PhotoArchive.Spikes.AppleDevice.Cli;

/// <summary>Minimal dependency-free parser: <c>command [positional...] [--name value] [--flag]</c>.</summary>
internal sealed class CommandLine
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    private CommandLine(string command, List<string> positionals)
    {
        Command = command;
        Positionals = positionals;
    }

    public string Command { get; }

    public List<string> Positionals { get; }

    internal static CommandLine Parse(IReadOnlyList<string> args)
    {
        var hasCommand = args.Count > 0 && !args[0].StartsWith("--", StringComparison.Ordinal);
        var command = hasCommand ? args[0].ToLowerInvariant() : "help";
        var positionals = new List<string>();
        var parsed = new CommandLine(command, positionals);
        for (var i = hasCommand ? 1 : 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg[2..];
                string? value = null;
                var eq = name.IndexOf('=', StringComparison.Ordinal);
                if (eq >= 0)
                {
                    value = name[(eq + 1)..];
                    name = name[..eq];
                }
                else if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }

                parsed._options[name] = value;
            }
            else
            {
                positionals.Add(arg);
            }
        }

        return parsed;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out var value) ? value : null;

    public int GetInt(string name, int fallback) =>
        _options.TryGetValue(name, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    public IEnumerable<string> OptionNames => _options.Keys;
}

internal sealed class ProbeUsageException(string message) : Exception(message);

internal sealed class ProbeAbortedException(string message) : Exception(message);
