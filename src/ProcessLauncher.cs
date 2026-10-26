using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Nover
{
    internal sealed class ProcessLauncher
    {
        public int Run(string executable, string[] arguments)
        {
            ProcessStartInfo start = CreateStartInfo(Path.GetDirectoryName(executable));
            if (IsCommandScript(executable))
            {
                // npm.cmd/npx.cmd 需要 cmd.exe 解释。
                string commandLine = QuoteWindowsArgument(executable);
                foreach (string argument in arguments)
                {
                    commandLine += " " + QuoteWindowsArgument(argument);
                }
                start.FileName = GetCommandProcessor();
                start.Arguments = "/d /s /c \"" + commandLine + "\"";
            }
            else
            {
                start.FileName = executable;
                start.Arguments = JoinWindowsArguments(arguments);
            }

            start.UseShellExecute = false;
            start.RedirectStandardInput = false;
            start.RedirectStandardOutput = false;
            start.RedirectStandardError = false;
            start.WorkingDirectory = Environment.CurrentDirectory;

            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        private static ProcessStartInfo CreateStartInfo(string versionPath)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            string existingPath = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            // 活动版本必须排在子进程 PATH 的最前面。
            start.EnvironmentVariables["PATH"] = versionPath + ";" + existingPath;
            return start;
        }

        private static bool IsCommandScript(string executable)
        {
            string extension = Path.GetExtension(executable);
            return String.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCommandProcessor()
        {
            string comspec = Environment.GetEnvironmentVariable("ComSpec");
            if (!String.IsNullOrWhiteSpace(comspec))
            {
                return comspec;
            }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe");
        }

        private static string JoinWindowsArguments(string[] arguments)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(QuoteWindowsArgument(argument));
            }
            return builder.ToString();
        }

        private static string QuoteWindowsArgument(string value)
        {
            // 按 CreateProcess/MSVCRT 规则转义参数。
            if (value == null)
            {
                return "\"\"";
            }
            if (value.Length > 0
                && value.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return value;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                }
                else if (character == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                }
                else
                {
                    builder.Append('\\', backslashes);
                    backslashes = 0;
                    builder.Append(character);
                }
            }
            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }
    }

    internal sealed class ShimManager
    {
        private readonly AppConfig _config;

        public string ShimDirectory
        {
            get { return _config.Home; }
        }

        public string NodeShimPath
        {
            get { return Path.Combine(ShimDirectory, "node.exe"); }
        }

        public ShimManager(AppConfig config)
        {
            _config = config;
        }

        public bool AreInstalled()
        {
            return File.Exists(Path.Combine(ShimDirectory, "nover.exe"))
                && File.Exists(NodeShimPath)
                && File.Exists(Path.Combine(ShimDirectory, "npm.cmd"))
                && File.Exists(Path.Combine(ShimDirectory, "npx.cmd"));
        }

        public void EnsureInstalled()
        {
            Directory.CreateDirectory(ShimDirectory);
            string currentExecutable = AppPaths.ExecutablePath;
            string currentName = Path.GetFileNameWithoutExtension(currentExecutable);
            if (String.Equals(currentName, "node", StringComparison.OrdinalIgnoreCase))
            {
                // 防止 node shim 递归覆盖自身。
                return;
            }

            string noverShimPath = Path.Combine(ShimDirectory, "nover.exe");
            if (!String.Equals(
                Path.GetFullPath(currentExecutable),
                Path.GetFullPath(noverShimPath),
                StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExecutable, noverShimPath, true);
            }
            // 依靠调用文件名进入 node 模式。
            File.Copy(noverShimPath, NodeShimPath, true);

            WriteWrapper("npm.cmd", "npm");
            WriteWrapper("npx.cmd", "npx");
        }

        private void WriteWrapper(string name, string command)
        {
            string path = Path.Combine(ShimDirectory, name);
            // 包装脚本专用的内部入口。
            string content =
                "@echo off\r\n"
                + "\"%~dp0nover.exe\" __shim " + command + " %*\r\n"
                + "exit /b %errorlevel%\r\n";
            File.WriteAllText(path, content);
        }
    }

    internal static class ShimDispatcher
    {
        public static int Run(string command, string[] arguments)
        {
            try
            {
                // 每次调用重新读取 active.txt。
                AppConfig config = AppConfig.Load(null, null);
                NodeReleaseClient releases = new NodeReleaseClient(config);
                GlobalVersionStore global = new GlobalVersionStore(config, releases);
                NodeVersion version = global.Read();
                if (version == null)
                {
                    throw new CliException(L.T("shim.no_active"));
                }

                string versionPath = config.VersionPath(version);
                // node 是 EXE，npm/npx 是 CMD。
                string target = Path.Combine(
                    versionPath,
                    String.Equals(command, "node", StringComparison.OrdinalIgnoreCase)
                        ? "node.exe"
                        : command + ".cmd");
                if (!File.Exists(target))
                {
                    throw new CliException(
                        L.T("shim.missing_target", version, Path.GetFileName(target)));
                }

                ProcessLauncher launcher = new ProcessLauncher();
                return launcher.Run(target, arguments);
            }
            catch (CliException ex)
            {
                Console.Error.WriteLine(L.T("shim.prefix") + " " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(L.T("shim.prefix") + " " + ex.Message);
                return 1;
            }
        }
    }
}
