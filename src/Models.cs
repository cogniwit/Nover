using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Nover
{
    internal static class L
    {
        private static readonly string RequestedLanguage = DetectRequestedLanguage();
        private static readonly string SelectedLanguage = DetectSelectedLanguage();
        private static readonly Dictionary<string, string> Messages = LoadMessages();
        private static readonly string HelpText = LoadHelpText();

        public static bool IsAvailable
        {
            get { return SelectedLanguage != null; }
        }

        public const string MissingLanguageMessage =
            "Language pack missing. Please download Nover again.";

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
                // 翻译格式错误时保留原文，不中断 CLI。
                return format;
            }
        }

        public static string Help(string version)
        {
            if (!IsAvailable)
            {
                return MissingLanguageMessage;
            }
            try
            {
                return String.Format(HelpText, version);
            }
            catch (FormatException)
            {
                return HelpText;
            }
        }

        private static string DetectRequestedLanguage()
        {
            string configured = PortableSettings.GetValue("lang");
            if (!String.IsNullOrWhiteSpace(configured)
                && !String.Equals(configured.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
            {
                return configured.Trim();
            }

            // auto 直接使用 Windows 界面区域性名称。
            string culture = CultureInfo.CurrentUICulture.Name;
            return String.IsNullOrWhiteSpace(culture) ? "default" : culture.Trim();
        }

        private static string DetectSelectedLanguage()
        {
            if (IsLanguagePack(RequestedLanguage))
            {
                return RequestedLanguage;
            }
            if (IsLanguagePack("default"))
            {
                return "default";
            }
            return null;
        }

        private static bool IsLanguagePack(string language)
        {
            // lang 只能是目录名，不能作为路径解析。
            if (String.IsNullOrWhiteSpace(language)
                || language == "."
                || language == ".."
                || language.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }
            string directory = LanguageDirectory(language);
            return Directory.Exists(directory)
                && File.Exists(Path.Combine(directory, "messages.ini"))
                && File.Exists(Path.Combine(directory, "help.txt"));
        }

        private static Dictionary<string, string> LoadMessages()
        {
            Dictionary<string, string> messages =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (SelectedLanguage == null)
            {
                return messages;
            }

            // default 提供基线，所选语言覆盖已有键。
            if (IsLanguagePack("default"))
            {
                OverlayMessages(messages, Path.Combine(LanguageDirectory("default"), "messages.ini"));
            }
            if (!String.Equals(SelectedLanguage, "default", StringComparison.OrdinalIgnoreCase))
            {
                OverlayMessages(messages, Path.Combine(LanguageDirectory(SelectedLanguage), "messages.ini"));
            }
            return messages;
        }

        private static void OverlayMessages(
            Dictionary<string, string> messages,
            string path)
        {
            foreach (KeyValuePair<string, string> pair in ParseMessages(File.ReadAllText(path)))
            {
                messages[pair.Key] = pair.Value;
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
                // 支持 \n、\r 转义。
                string value = line.Substring(separator + 1).Trim()
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r");
                messages[key] = value;
            }
            return messages;
        }

        private static string LoadHelpText()
        {
            if (SelectedLanguage == null)
            {
                return MissingLanguageMessage;
            }
            string selectedPath = Path.Combine(LanguageDirectory(SelectedLanguage), "help.txt");
            if (File.Exists(selectedPath))
            {
                return File.ReadAllText(selectedPath);
            }
            string defaultPath = Path.Combine(LanguageDirectory("default"), "help.txt");
            return File.Exists(defaultPath)
                ? File.ReadAllText(defaultPath)
                : MissingLanguageMessage;
        }

        private static string LanguageDirectory(string language)
        {
            return Path.Combine(
                AppPaths.ExecutableDirectory,
                "lang",
                language);
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
