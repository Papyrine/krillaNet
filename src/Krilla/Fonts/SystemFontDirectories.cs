using System.Text.RegularExpressions;

/// <summary>
/// Where each platform keeps its installed fonts.
/// </summary>
/// <remarks>
/// Only directories that exist are returned, so the list doubles as a diagnostic: printed on a
/// machine that finds nothing, it says where the scan looked.
/// </remarks>
static partial class SystemFontDirectories
{
    public static IReadOnlyList<string> Resolve()
    {
        var directories = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            Add(directories, Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            // Fonts installed "for this user only" land here, and Windows itself searches it.
            Add(directories, Path.Combine(LocalAppData(), "Microsoft", "Windows", "Fonts"));
        }
        else if (OperatingSystem.IsMacOS() ||
                 OperatingSystem.IsMacCatalyst())
        {
            // Recursive, so /System/Library/Fonts/Supplemental — where Arial and Times New Roman
            // live on a current macOS — is covered by its parent.
            Add(directories, "/System/Library/Fonts");
            Add(directories, "/Library/Fonts");
            Add(directories, "/Network/Library/Fonts");
            Add(directories, Path.Combine(Home(), "Library", "Fonts"));
        }
        else if (OperatingSystem.IsAndroid())
        {
            Add(directories, "/system/fonts");
        }
        else if (!OperatingSystem.IsBrowser() &&
                 !OperatingSystem.IsWasi())
        {
            foreach (var directory in Fontconfig())
            {
                Add(directories, directory);
            }

            // The XDG and legacy defaults, for a system with no fontconfig configuration or one
            // that does not list them.
            Add(directories, Path.Combine(XdgDataHome(), "fonts"));
            Add(directories, Path.Combine(Home(), ".fonts"));
            foreach (var data in XdgDataDirs())
            {
                Add(directories, Path.Combine(data, "fonts"));
            }

            Add(directories, "/usr/share/fonts");
            Add(directories, "/usr/local/share/fonts");
        }

        return directories;
    }

    static void Add(List<string> directories, string directory)
    {
        // Not rooted means a component such as the home directory came back empty, and resolving
        // what is left against the working directory would scan somewhere arbitrary.
        if (!Path.IsPathRooted(directory))
        {
            return;
        }

        string full;
        try
        {
            full = Path.GetFullPath(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (!Directory.Exists(full) ||
            directories.Contains(full, StringComparer.Ordinal))
        {
            return;
        }

        directories.Add(full);
    }

    /// <summary>
    /// The <c>&lt;dir&gt;</c> entries in the system fontconfig configuration.
    /// </summary>
    /// <remarks>
    /// Read with a pattern rather than an XML parser: <c>fonts.conf</c> declares a DOCTYPE, which
    /// the framework's parser refuses by default, and a <c>&lt;dir&gt;</c> element is all that is
    /// wanted out of it. <c>&lt;include&gt;</c> is not followed beyond <c>conf.d</c>, the one
    /// directory every distribution includes.
    /// </remarks>
    static IEnumerable<string> Fontconfig()
    {
        var files = new List<string>
        {
            "/etc/fonts/fonts.conf",
            "/etc/fonts/local.conf"
        };

        try
        {
            if (Directory.Exists("/etc/fonts/conf.d"))
            {
                var included = Directory.GetFiles("/etc/fonts/conf.d", "*.conf");
                Array.Sort(included, StringComparer.Ordinal);
                files.AddRange(included);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        foreach (var file in files)
        {
            string text;
            try
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                text = File.ReadAllText(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (Match match in DirElement().Matches(text))
            {
                var path = match.Groups["path"].Value.Trim();
                var prefix = PrefixAttribute().Match(match.Groups["attributes"].Value).Groups["prefix"].Value;

                if (path.Length == 0)
                {
                    continue;
                }

                if (prefix == "xdg")
                {
                    yield return Path.Combine(XdgDataHome(), path);
                }
                else if (prefix == "relative")
                {
                    yield return Path.Combine(Path.GetDirectoryName(file) ?? "/", path);
                }
                else if (path.StartsWith('~'))
                {
                    yield return Home() + path[1..];
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    [GeneratedRegex(@"<dir\b(?<attributes>[^>]*)>(?<path>[^<]*)</dir>")]
    private static partial Regex DirElement();

    [GeneratedRegex("""prefix\s*=\s*["'](?<prefix>[^"']*)["']""")]
    private static partial Regex PrefixAttribute();

    static string Home() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    static string LocalAppData() =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    static string XdgDataHome()
    {
        var value = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(value))
        {
            return Path.Combine(Home(), ".local", "share");
        }

        return value;
    }

    static string[] XdgDataDirs()
    {
        var value = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
        if (string.IsNullOrEmpty(value))
        {
            return ["/usr/local/share", "/usr/share"];
        }

        return value.Split(':', StringSplitOptions.RemoveEmptyEntries);
    }
}
