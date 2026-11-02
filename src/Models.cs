using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Nover
{
    internal static class L
    {
        public static string SystemLanguage
        {
            get
            {
                string culture = CultureInfo.CurrentUICulture.Name;
                if (String.Equals(culture, "zh", StringComparison.OrdinalIgnoreCase)
                    || culture.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
                {
                    return "Chinese";
                }
                return "English";
            }
        }

        private static Dictionary<string, string> Messages =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static string HelpText = String.Empty;

        public static string SelectedLanguage { get; private set; }

        public static bool Load(string language)
        {
            string root = Path.Combine(
                AppPaths.ExecutableDirectory,
                "lang",
                language);
            string messagesPath = Path.Combine(root, "messages.ini");
            string helpPath = Path.Combine(root, "help.txt");
            if (!File.Exists(messagesPath) || !File.Exists(helpPath))
            {
                Messages = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
                HelpText = String.Empty;
                SelectedLanguage = null;
                return false;
            }

            Messages = ParseMessages(File.ReadAllText(messagesPath));
            HelpText = File.ReadAllText(helpPath);
            SelectedLanguage = language;
            return true;
        }

        public static bool LoadFallback(
            string preferred,
            out string selected,
            out bool usedFallback)
        {
            usedFallback = false;
            if (!String.IsNullOrWhiteSpace(preferred) && Load(preferred))
            {
                selected = preferred;
                return true;
            }

            List<string> candidates = new List<string>();
            candidates.Add("English");
            candidates.AddRange(GetAvailableLanguages());
            HashSet<string> tried = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            if (!String.IsNullOrWhiteSpace(preferred))
            {
                tried.Add(preferred);
            }

            foreach (string candidate in candidates)
            {
                if (candidate.Length == 0 || !tried.Add(candidate))
                {
                    continue;
                }
                if (Load(candidate))
                {
                    selected = candidate;
                    usedFallback = true;
                    return true;
                }
            }

            selected = null;
            return false;
        }

        public static List<string> GetAvailableLanguages()
        {
            List<string> languages = new List<string>();
            string root = Path.Combine(AppPaths.ExecutableDirectory, "lang");
            if (!Directory.Exists(root))
            {
                return languages;
            }
            foreach (string directory in Directory.GetDirectories(root))
            {
                if (File.Exists(Path.Combine(directory, "messages.ini"))
                    && File.Exists(Path.Combine(directory, "help.txt")))
                {
                    languages.Add(Path.GetFileName(directory));
                }
            }
            languages.Sort(StringComparer.OrdinalIgnoreCase);
            return languages;
        }

        public static string T(string key, params object[] values)
        {
            string format;
            if (!Messages.TryGetValue(key, out format))
            {
                return "[" + key + "]";
            }
            try
            {
                return values == null || values.Length == 0
                    ? format
                    : String.Format(format, values);
            }
            catch (FormatException)
            {
                // 格式字符串错误时保留原文。
                return format;
            }
        }

        public static string Help(string version)
        {
            try
            {
                return String.Format(HelpText, version);
            }
            catch (FormatException)
            {
                return HelpText;
            }
        }

        private static Dictionary<string, string> ParseMessages(string content)
        {
            Dictionary<string, string> messages =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in content.Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim()
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r");
                messages[key] = value;
            }
            return messages;
        }

    }

    internal sealed class CliException : Exception
    {
        public CliException(string message) : base(message)
        {
        }
    }

    internal sealed class NodeVersion : IComparable<NodeVersion>, IEquatable<NodeVersion>
    {
        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }

        public NodeVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static bool TryParse(string value, out NodeVersion version, out int componentCount)
        {
            // 保留输入层级：20、20.20、20.20.2 的匹配范围不同。
            version = null;
            componentCount = 0;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string text = value.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            string[] parts = text.Split('.');
            if (parts.Length < 1 || parts.Length > 3)
            {
                return false;
            }

            int[] values = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!Int32.TryParse(parts[i], out values[i]) || values[i] < 0)
                {
                    return false;
                }
            }

            componentCount = parts.Length;
            version = new NodeVersion(values[0], values[1], values[2]);
            return true;
        }

        public bool MatchesPrefix(NodeVersion prefix, int componentCount)
        {
            if (componentCount >= 1 && Major != prefix.Major)
            {
                return false;
            }
            if (componentCount >= 2 && Minor != prefix.Minor)
            {
                return false;
            }
            if (componentCount >= 3 && Patch != prefix.Patch)
            {
                return false;
            }
            return true;
        }

        public int CompareTo(NodeVersion other)
        {
            if (other == null)
            {
                return 1;
            }
            int result = Major.CompareTo(other.Major);
            if (result != 0)
            {
                return result;
            }
            result = Minor.CompareTo(other.Minor);
            if (result != 0)
            {
                return result;
            }
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(NodeVersion other)
        {
            return other != null && Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeVersion);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major;
                hash = (hash * 397) ^ Minor;
                hash = (hash * 397) ^ Patch;
                return hash;
            }
        }

        public override string ToString()
        {
            return Major + "." + Minor + "." + Patch;
        }
    }

    internal sealed class NodeRelease
    {
        public NodeVersion Version { get; set; }
        public bool IsLts { get; set; }
        public HashSet<string> Files { get; private set; }

        public NodeRelease()
        {
            Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool HasArchive(string architecture)
        {
            return Files.Contains("win-" + architecture + "-zip");
        }
    }

    internal sealed class VersionConstraint
    {
        // 仅支持数字前缀，不支持 semver 范围语法。
        private readonly NodeVersion _version;
        private readonly int _componentCount;

        public string Text { get; private set; }

        private VersionConstraint(
            string text,
            NodeVersion version,
            int componentCount)
        {
            Text = text;
            _version = version;
            _componentCount = componentCount;
        }

        public bool Matches(NodeVersion version)
        {
            return version.MatchesPrefix(_version, _componentCount);
        }

        public static VersionConstraint Parse(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                throw new CliException(L.T("version.empty"));
            }

            string text = value.Trim();
            if (text.IndexOf("lts/", StringComparison.OrdinalIgnoreCase) >= 0
                || String.Equals(text, "lts", StringComparison.OrdinalIgnoreCase))
            {
                // install 单独处理 lts；use/remove 只接受数字版本。
                throw new CliException(L.T("version.lts_alias"));
            }
            // 先排除范围运算符和版本后缀。
            if (text.IndexOfAny(new char[] { ' ', '\t', ',', '-', '+', '<', '>', '=', '~', '^', '|' }) >= 0)
            {
                throw new CliException(L.T("version.invalid_requirement", value));
            }

            string[] parts = text.TrimStart('v', 'V').Split('.');
            if (parts.Length < 1 || parts.Length > 3)
            {
                throw new CliException(L.T("version.invalid_requirement", value));
            }

            int[] numbers = new int[3];
            int componentCount = 0;
            foreach (string part in parts)
            {
                if (!Int32.TryParse(part, out numbers[componentCount])
                    || numbers[componentCount] < 0)
                {
                    throw new CliException(L.T("version.invalid_requirement", value));
                }
                componentCount++;
            }

            return new VersionConstraint(
                text,
                new NodeVersion(numbers[0], numbers[1], numbers[2]),
                componentCount);
        }
    }
}
