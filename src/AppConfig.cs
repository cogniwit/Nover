using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Nover
{
    internal static class AppPaths
    {
        public static readonly string ExecutablePath =
            Process.GetCurrentProcess().MainModule.FileName;

        public static readonly string ExecutableDirectory =
            Path.GetDirectoryName(ExecutablePath);
    }

    internal static class PortableSettings
    {
        public static string ConfigPath
        {
            get
            {
                return Path.Combine(AppPaths.ExecutableDirectory, "nover.config");
            }
        }

        public static string GetValue(string key)
        {
            // 仅支持注释、key=value，键名不区分大小写。
            string path = ConfigPath;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(path))
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
                    string name = line.Substring(0, separator).Trim();
                    if (!String.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    return line.Substring(separator + 1).Trim().Trim('"');
                }
            }
            catch
            {
                return null;
            }
            return null;
        }

        public static void SetValue(string key, string value)
        {
            string path = ConfigPath;
            List<string> lines = File.Exists(path)
                ? new List<string>(File.ReadAllLines(path))
                : new List<string>();
            bool updated = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }
                string name = line.Substring(0, separator).Trim();
                if (String.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + "=" + (value ?? String.Empty);
                    updated = true;
                    break;
                }
            }
            if (!updated)
            {
                lines.Add(key + "=" + (value ?? String.Empty));
            }
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        public static void EnsureTemplate(string root)
        {
            // 只补缺失文件，不覆盖已有配置。
            string path = Path.Combine(root, "nover.config");
            if (File.Exists(path))
            {
                return;
            }
            File.WriteAllText(
                path,
                "# Nover portable settings\r\n"
                + "# Nover 便携配置\r\n"
                + "# Must be located next to nover.exe.\r\n"
                + "# 本文件必须与 nover.exe 位于同一目录。\r\n"
                + "# One setting per line. Comments must use their own line.\r\n"
                + "# 每项单独一行；注释必须独占一行，不能写在值后面。\r\n"
                + "# Priority: command line > this file > detected defaults.\r\n"
                + "# 优先级：命令行参数 > 本文件 > 自动检测默认值。\r\n"
                + "\r\n"
                + "# Language: auto, or an exact directory under lang\\ (for example zh-CN).\r\n"
                + "# 语言：auto 跟随 Windows；或填写 lang\\ 下的精确目录名，例如 zh-CN。\r\n"
                + "# If no exact match exists, lang\\default is used.\r\n"
                + "# 未精确匹配时使用 lang\\default。\r\n"
                + "lang=auto\r\n"
                + "\r\n"
                + "# Architecture: auto, x64, arm64, or x86.\r\n"
                + "# 架构：auto 自动识别，也可填写 x64、arm64 或 x86。\r\n"
                + "arch=auto\r\n"
                + "\r\n"
                + "# Node.js distribution mirror. Use an absolute HTTP/HTTPS URL.\r\n"
                + "# Node.js 下载镜像：请填写绝对的 HTTP/HTTPS URL。\r\n"
                + "mirror=https://nodejs.org/dist/\r\n"
                + "\r\n"
                + "# Current installation path. Managed automatically by Nover.\r\n"
                + "# 当前安装目录：由 Nover 自动维护，请勿手动修改。\r\n"
                + "install_path=\r\n");
        }
    }

    internal sealed class AppConfig
    {
        public string Home { get; private set; }
        public string VersionsPath { get; private set; }
        public string Mirror { get; private set; }
        public string Architecture { get; private set; }

        private AppConfig()
        {
        }

        public static AppConfig Load(string mirrorOverride, string architectureOverride)
        {
            AppConfig config = new AppConfig();
            config.Home = Path.GetFullPath(AppPaths.ExecutableDirectory);
            config.VersionsPath = Path.Combine(config.Home, "versions");
            string configMirror = PortableSettings.GetValue("mirror");
            string configArchitecture = PortableSettings.GetValue("arch");
            if (String.Equals(configArchitecture, "auto", StringComparison.OrdinalIgnoreCase))
            {
                configArchitecture = null;
            }
            config.Mirror = EnsureTrailingSlash(FirstNonEmpty(
                mirrorOverride,
                configMirror,
                "https://nodejs.org/dist/"));
            config.Architecture = NormalizeArchitecture(FirstNonEmpty(
                architectureOverride,
                configArchitecture,
                DetectArchitecture()));
            return config;
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(VersionsPath);
        }

        public string VersionPath(NodeVersion version)
        {
            // 路径必须位于受管的 versions 目录内。
            return Safety.EnsureChildPath(VersionsPath, Path.Combine(VersionsPath, "v" + version));
        }

        public string ReleaseArchiveName(NodeVersion version, string architecture)
        {
            return "node-v" + version + "-win-" + architecture + ".zip";
        }

        public string ReleaseBaseUrl(NodeVersion version)
        {
            return CombineUrl(Mirror, "v" + version + "/");
        }

        public static string CombineUrl(string baseUrl, string relative)
        {
            Uri root = new Uri(EnsureTrailingSlash(baseUrl), UriKind.Absolute);
            return new Uri(root, relative).AbsoluteUri;
        }

        public static string DetectArchitecture()
        {
            // WOW64 下优先取真实系统架构。
            string architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
            if (String.IsNullOrWhiteSpace(architecture))
            {
                architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            }
            return NormalizeArchitecture(architecture);
        }

        public static string NormalizeArchitecture(string value)
        {
            string architecture = (value ?? String.Empty).Trim().ToLowerInvariant();
            if (architecture == "amd64" || architecture == "x64" || architecture == "64")
            {
                return "x64";
            }
            if (architecture == "arm64" || architecture == "aarch64")
            {
                return "arm64";
            }
            if (architecture == "x86" || architecture == "i386" || architecture == "32")
            {
                return "x86";
            }
            throw new CliException(
                L.T("setup.unsupported_arch", value));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!String.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
            return String.Empty;
        }

        private static string EnsureTrailingSlash(string value)
        {
            string path = value.Trim();
            return path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        }
    }

    internal static class SelfInstaller
    {
        public static bool EnsureReady(out bool configurationChanged)
        {
            configurationChanged = false;
            string root = AppPaths.ExecutableDirectory;
            string previousPath = PortableSettings.GetValue("install_path");
            bool moved = !String.IsNullOrWhiteSpace(previousPath)
                && !String.Equals(
                    NormalizePathEntry(previousPath),
                    NormalizePathEntry(root),
                    StringComparison.OrdinalIgnoreCase);

            // 已在 PATH 中只做修复；目录移动时仍要用户确认。
            if (!moved && IsPathConfigured(root))
            {
                bool configUpdated = !String.Equals(
                    NormalizePathEntry(previousPath),
                    NormalizePathEntry(root),
                    StringComparison.OrdinalIgnoreCase);
                if (configUpdated)
                {
                    PortableSettings.EnsureTemplate(root);
                }

                AppConfig config = AppConfig.Load(null, null);
                ShimManager shims = new ShimManager(config);
                bool shimsRepaired = !shims.AreInstalled();
                if (shimsRepaired)
                {
                    shims.EnsureInstalled();
                }

                if (configUpdated)
                {
                    PortableSettings.SetValue("install_path", root);
                    configurationChanged = true;
                    Console.WriteLine(L.T(
                        shimsRepaired
                            ? "setup.repaired_and_config"
                            : "setup.already_configured",
                        root));
                }
                else if (shimsRepaired)
                {
                    configurationChanged = true;
                    Console.WriteLine(L.T("setup.repaired", root));
                }
                return true;
            }

            if (moved)
            {
                Console.Write(
                    L.T("setup.moved", previousPath, root));
            }
            else
            {
                Console.Write(
                    L.T("setup.required", root));
            }
            while (true)
            {
                // EOF 与无效输入分开处理，避免循环等待。
                string response = Console.ReadLine();
                if (response == null)
                {
                    Console.WriteLine(L.T("setup.cancelled.newline"));
                    return false;
                }

                string answer = response.Trim().ToLowerInvariant();
                if (answer == "y" || answer == "yes")
                {
                    Install(root);
                    configurationChanged = true;
                    return true;
                }
                if (answer == "n" || answer == "no")
                {
                    Console.WriteLine(L.T("setup.cancelled"));
                    return false;
                }
                Console.Write(L.T("setup.retry"));
            }
        }

        private static bool IsPathConfigured(string directory)
        {
            string normalizedDirectory = NormalizePathEntry(directory);
            return IsDirectoryOnPath(
                    Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User),
                    normalizedDirectory)
                || IsDirectoryOnPath(
                    Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process),
                    normalizedDirectory);
        }

        private static bool IsDirectoryOnPath(string pathValue, string normalizedDirectory)
        {
            foreach (string entry in (pathValue ?? String.Empty).Split(';'))
            {
                if (String.Equals(
                    NormalizePathEntry(entry),
                    normalizedDirectory,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Install(string root)
        {
            Directory.CreateDirectory(root);
            PortableSettings.EnsureTemplate(root);

            List<string> previousBins = new List<string>();
            string configuredPath = PortableSettings.GetValue("install_path");
            if (!String.IsNullOrWhiteSpace(configuredPath))
            {
                previousBins.Add(configuredPath.Trim());
            }

            string userPath = Environment.GetEnvironmentVariable(
                "Path",
                EnvironmentVariableTarget.User) ?? String.Empty;
            List<string> cleaned = CleanPathEntries(
                userPath,
                root,
                previousBins);
            // 放在 PATH 最前，确保 Nover shim 优先。
            string updatedPath = cleaned.Count == 0
                ? root
                : root + ";" + String.Join(";", cleaned);
            Environment.SetEnvironmentVariable(
                "Path",
                updatedPath,
                EnvironmentVariableTarget.User);
            PortableSettings.SetValue("install_path", root);

            AppConfig config = AppConfig.Load(null, null);
            ShimManager shims = new ShimManager(config);
            shims.EnsureInstalled();

            Console.WriteLine(
                L.T("setup.initialized", root));
        }

        private static List<string> CleanPathEntries(
            string pathValue,
            string newDirectory,
            List<string> previousBins)
        {
            string normalizedNew = NormalizePathEntry(newDirectory);
            HashSet<string> knownOldPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string previousBin in previousBins)
            {
                string normalized = NormalizePathEntry(previousBin);
                if (normalized.Length > 0)
                {
                    knownOldPaths.Add(normalized);
                }
            }

            // 只移除空项、重复项和已知 Nover 路径。
            List<string> result = new List<string>();
            foreach (string rawEntry in pathValue.Split(';'))
            {
                string entry = NormalizePathEntry(rawEntry);
                if (entry.Length == 0
                    || String.Equals(entry, normalizedNew, StringComparison.OrdinalIgnoreCase)
                    || knownOldPaths.Contains(entry))
                {
                    continue;
                }
                result.Add(rawEntry.Trim().Trim('"'));
            }
            return result;
        }

        private static string NormalizePathEntry(string value)
        {
            return (value ?? String.Empty)
                .Trim()
                .Trim('"')
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    internal static class Safety
    {
        // 删除前同时检查路径边界和重解析点。
        private const uint InvalidFileAttributes = 0xFFFFFFFF;
        private const uint FileAttributeReparsePoint = 0x400;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributes(string fileName);

        public static string EnsureChildPath(string parent, string candidate)
        {
            // 规范化后比较，阻止 ".." 和绝对路径逃逸。
            string normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate);
            if (!normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                throw new CliException(
                    L.T("safety.outside_path", candidate));
            }
            return normalizedCandidate;
        }

        public static bool PathExists(string path)
        {
            return GetFileAttributes(path) != InvalidFileAttributes;
        }

        public static bool IsReparsePoint(string path)
        {
            uint attributes = GetFileAttributes(path);
            if (attributes == InvalidFileAttributes)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 2 || error == 3)
                {
                    return false;
                }
                throw new IOException(L.T("safety.inspect_error", path, error));
            }
            return (attributes & FileAttributeReparsePoint) != 0;
        }

        public static void DeleteManagedDirectory(string path, string parent)
        {
            // 递归删除前确认路径和链接类型。
            string safePath = EnsureChildPath(parent, path);
            if (!PathExists(safePath))
            {
                throw new CliException(
                    L.T("safety.missing_directory", safePath));
            }
            if (IsReparsePoint(safePath))
            {
                throw new CliException(
                    L.T("safety.reparse", safePath));
            }
            Directory.Delete(safePath, true);
        }
    }
}
