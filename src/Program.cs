using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

[assembly: AssemblyTitle("Nover")]
[assembly: AssemblyDescription("Node.js version manager for Windows")]
[assembly: AssemblyProduct("Nover")]
[assembly: AssemblyVersion(Nover.Program.FileVersion)]
[assembly: AssemblyFileVersion(Nover.Program.FileVersion)]
[assembly: AssemblyInformationalVersion(Nover.Program.ToolVersion)]

namespace Nover
{
    internal static class Program
    {
        internal const string ToolVersion = "1.0.1";
        internal const string FileVersion = ToolVersion + ".0";

        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                if (!L.IsAvailable)
                {
                    Console.Error.WriteLine(L.MissingLanguageMessage);
                    return 1;
                }
                string invokedName = Path.GetFileNameWithoutExtension(
                    Environment.GetCommandLineArgs()[0]);
                // node.exe 是 nover.exe 的副本，按被调用文件名分流。
                if (String.Equals(invokedName, "node", StringComparison.OrdinalIgnoreCase))
                {
                    return ShimDispatcher.Run("node", args);
                }
                // npm.cmd/npx.cmd 的私有转发入口。
                if (args.Length > 0
                    && String.Equals(args[0], "__shim", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < 2)
                    {
                        Console.Error.WriteLine(L.T("shim.missing_command"));
                        return 1;
                    }
                    string[] forwarded = new string[args.Length - 2];
                    Array.Copy(args, 2, forwarded, 0, forwarded.Length);
                    return ShimDispatcher.Run(args[1], forwarded);
                }

                bool informational = args.Length == 1
                    && (String.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase)
                        || String.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase));
                bool configurationChanged = false;
                if (!informational && !SelfInstaller.EnsureReady(out configurationChanged))
                {
                    return 1;
                }

                if (args.Length == 0)
                {
                    if (configurationChanged)
                    {
                        // 留出时间查看初始化结果，避免窗口直接关闭。
                        Console.ReadLine();
                        return 0;
                    }
                    PrintHelp();
                    return 1;
                }

                Options options = Options.Parse(args);
                string command = options.Command.ToLowerInvariant();
                if (command == "--help")
                {
                    PrintHelp();
                    return 0;
                }
                if (command == "--version")
                {
                    Console.WriteLine(ToolVersion);
                    return 0;
                }

                AppConfig config = AppConfig.Load(options.Mirror, options.Architecture);
                NodeReleaseClient releases = new NodeReleaseClient(config);
                Installer installer = new Installer(config, releases);
                GlobalVersionStore global = new GlobalVersionStore(config, releases);
                ShimManager shims = new ShimManager(config);

                switch (command)
                {
                    case "install":
                        RequireArgumentCount(options, 1, 1, "install <latest|lts|version> [--use]");
                        NodeVersion installed = installer.Install(
                            options.Positionals[0],
                            config.Architecture);
                        if (options.UseAfterInstall)
                        {
                            global.Write(installed);
                            shims.EnsureInstalled();
                            Console.WriteLine(L.T("command.global_now", installed));
                        }
                        break;

                    case "use":
                        RequireArgumentCount(options, 1, 1, "use <version>");
                        NodeVersion active = global.Use(options.Positionals[0]);
                        shims.EnsureInstalled();
                        Console.WriteLine(L.T("command.global_now", active));
                        break;

                    case "list":
                    case "ls":
                        RequireArgumentCount(options, 0, 0, "list");
                        ListInstalled(releases, global);
                        break;

                    case "remote":
                        RequireArgumentCount(
                            options,
                            0,
                            1,
                            "remote [major] [--lts] [--limit N|--all] [--offset N]");
                        ListRemote(releases, options);
                        break;

                    case "remove":
                    case "rm":
                        RequireArgumentCount(options, 1, 1, "remove <version>");
                        RemoveVersion(config, releases, global, options.Positionals[0]);
                        break;

                    case "check":
                        RequireArgumentCount(options, 0, 0, "check");
                        return RunCheck(config, releases, global, shims);

                    default:
                        throw new CliException(L.T("command.unknown", args[0]));
                }
                return 0;
            }
            catch (CliException ex)
            {
                Console.Error.WriteLine(L.T("error.prefix") + " " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(L.T("error.prefix") + " " + ex.Message);
                return 1;
            }
        }

        private static void ListInstalled(
            NodeReleaseClient releases,
            GlobalVersionStore global)
        {
            NodeVersion active = global.Read();
            Console.WriteLine(L.T(
                "list.current_value",
                active == null ? L.T("common.none") : "v" + active));
            Console.WriteLine();
            Console.WriteLine(L.T("list.installed"));

            List<NodeVersion> installed = releases.GetInstalled();
            if (installed.Count == 0)
            {
                Console.WriteLine("  " + L.T("common.none"));
                return;
            }

            foreach (NodeVersion version in installed)
            {
                Console.WriteLine("  " + version);
            }
        }

        private static void ListRemote(NodeReleaseClient releases, Options options)
        {
            if (options.AllVersions && options.LimitSpecified)
            {
                throw new CliException(L.T("remote.all_limit_conflict"));
            }

            int? major = ParseMajorFilter(options);
            List<NodeRelease> filtered = new List<NodeRelease>();
            // NodeReleaseClient 已按版本降序排列。
            foreach (NodeRelease release in releases.GetReleases())
            {
                if ((!options.LtsOnly || release.IsLts)
                    && (!major.HasValue || release.Version.Major == major.Value))
                {
                    filtered.Add(release);
                }
            }

            if (options.Offset >= filtered.Count)
            {
                Console.WriteLine(L.T("remote.no_offset", options.Offset, filtered.Count));
                return;
            }

            int end = options.AllVersions
                ? filtered.Count
                : Math.Min(options.Offset + options.Limit, filtered.Count);
            Console.WriteLine(L.T("remote.header"));
            for (int i = options.Offset; i < end; i++)
            {
                NodeRelease release = filtered[i];
                Console.WriteLine(
                    "v" + release.Version.ToString().PadRight(10)
                    + (release.IsLts ? "LTS" : L.T("remote.current")));
            }
            Console.WriteLine(
                L.T("remote.showing", options.Offset + 1, end, filtered.Count));
            if (end < filtered.Count)
            {
                Console.WriteLine(L.T("remote.more", end));
            }
        }

        private static int? ParseMajorFilter(Options options)
        {
            if (options.Positionals.Count == 1
                && !String.IsNullOrWhiteSpace(options.MajorFilter))
            {
                throw new CliException(L.T("remote.major_conflict"));
            }
            string value = options.MajorFilter;
            if (String.IsNullOrWhiteSpace(value) && options.Positionals.Count == 1)
            {
                value = options.Positionals[0];
            }
            if (String.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // 主版本可带 v 前缀，但不接受通配符。
            string text = value.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            int major;
            if (!Int32.TryParse(text, out major) || major < 0 || major > 999)
            {
                throw new CliException(L.T("remote.major_invalid"));
            }
            return major;
        }

        private static void RemoveVersion(
            AppConfig config,
            NodeReleaseClient releases,
            GlobalVersionStore global,
            string specification)
        {
            NodeVersion version = releases.ResolveInstalled(VersionConstraint.Parse(specification));
            NodeVersion active = global.Read();
            if (active != null && active.Equals(version))
            {
                throw new CliException(L.T("remove.active_denied"));
            }

            Safety.DeleteManagedDirectory(config.VersionPath(version), config.VersionsPath);
            Console.WriteLine(L.T("remove.done", version));
        }

        private static int RunCheck(
            AppConfig config,
            NodeReleaseClient releases,
            GlobalVersionStore global,
            ShimManager shims)
        {
            List<string> problems = new List<string>();
            Console.WriteLine("Nover " + ToolVersion);
            Console.WriteLine(L.T("check.home") + "     " + config.Home);
            Console.WriteLine(L.T("check.versions") + " " + config.VersionsPath);
            Console.WriteLine(L.T("check.shims") + "    " + shims.ShimDirectory);
            Console.WriteLine(L.T("check.mirror") + "   " + config.Mirror);
            Console.WriteLine(L.T("check.arch") + "     " + config.Architecture);
            Console.WriteLine();

            try
            {
                config.EnsureDirectories();
                string probe = Path.Combine(config.Home, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                Console.WriteLine(L.T("check.ok_writable"));
            }
            catch (Exception ex)
            {
                problems.Add(L.T("check.error_writable", ex.Message));
            }

            List<NodeVersion> installed = releases.GetInstalled();
            Console.WriteLine(L.T("check.info_installed", installed.Count));
            foreach (NodeVersion version in installed)
            {
                string path = config.VersionPath(version);
                if (!File.Exists(Path.Combine(path, "node.exe")))
                {
                    problems.Add(L.T("check.missing_node", version));
                }
                if (!File.Exists(Path.Combine(path, "npm.cmd")))
                {
                    Console.WriteLine(L.T("check.warn_npm", version));
                }
            }

            NodeVersion active = global.Read();
            if (active == null)
            {
                Console.WriteLine(L.T("check.info_no_global"));
            }
            else
            {
                Console.WriteLine(L.T("check.ok_global", active));
                if (!File.Exists(shims.NodeShimPath))
                {
                    problems.Add(L.T("check.missing_shim", active));
                }
                else
                {
                    Console.WriteLine(L.T("check.ok_shim"));
                }
            }

            string firstNode = FindFirstNodeOnPath();
            if (!String.IsNullOrWhiteSpace(firstNode))
            {
                if (String.Equals(
                    Path.GetFullPath(firstNode),
                    Path.GetFullPath(shims.NodeShimPath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(L.T("check.ok_resolves"));
                }
                else if (active != null)
                {
                    problems.Add(L.T("check.path_mismatch", firstNode, shims.NodeShimPath));
                }
            }
            else if (active != null)
            {
                problems.Add(L.T("check.no_node"));
            }

            if (problems.Count == 0)
            {
                Console.WriteLine("\n" + L.T("check.ok"));
                return 0;
            }

            Console.WriteLine("\n" + L.T("check.problems"));
            foreach (string problem in problems)
            {
                Console.WriteLine(" - " + problem);
            }
            return 1;
        }

        private static string FindFirstNodeOnPath()
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (string entry in pathValue.Split(';'))
            {
                if (String.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }
                string candidate = Path.Combine(entry.Trim().Trim('"'), "node.exe");
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            return null;
        }

        private static void RequireArgumentCount(Options options, int minimum, int maximum, string usage)
        {
            if (options.Positionals.Count < minimum || options.Positionals.Count > maximum)
            {
                throw new CliException(L.T("usage", usage));
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine(L.Help(ToolVersion));
        }

        private sealed class Options
        {
            public string Command { get; private set; }
            public List<string> Positionals { get; private set; }
            public string Mirror { get; private set; }
            public string Architecture { get; private set; }
            public bool UseAfterInstall { get; private set; }
            public bool LtsOnly { get; private set; }
            public bool AllVersions { get; private set; }
            public bool LimitSpecified { get; private set; }
            public string MajorFilter { get; private set; }
            public int Offset { get; private set; }
            public int Limit { get; private set; }

            private Options()
            {
                Positionals = new List<string>();
                Limit = 20;
            }

            public static Options Parse(string[] args)
            {
                Options options = new Options();
                // 第一个参数是命令，其余选项可穿插在位置参数中。
                options.Command = args[0];
                for (int i = 1; i < args.Length; i++)
                {
                    string argument = args[i];
                    if (argument == "--mirror")
                    {
                        options.Mirror = ReadOptionValue(args, ref i, "--mirror");
                    }
                    else if (argument == "--arch")
                    {
                        options.Architecture = ReadOptionValue(args, ref i, "--arch");
                    }
                    else if (argument == "--use")
                    {
                        options.UseAfterInstall = true;
                    }
                    else if (argument == "--lts")
                    {
                        options.LtsOnly = true;
                    }
                    else if (argument == "--all")
                    {
                        options.AllVersions = true;
                    }
                    else if (argument == "--major")
                    {
                        options.MajorFilter = ReadOptionValue(args, ref i, "--major");
                    }
                    else if (argument == "--offset")
                    {
                        string value = ReadOptionValue(args, ref i, "--offset");
                        int offset;
                        if (!Int32.TryParse(value, out offset) || offset < 0)
                        {
                            throw new CliException(L.T("option.offset"));
                        }
                        options.Offset = offset;
                    }
                    else if (argument == "--limit")
                    {
                        string value = ReadOptionValue(args, ref i, "--limit");
                        int limit;
                        if (!Int32.TryParse(value, out limit) || limit < 1 || limit > 5000)
                        {
                            throw new CliException(L.T("option.limit"));
                        }
                        options.Limit = limit;
                        options.LimitSpecified = true;
                    }
                    else if (argument == "--help")
                    {
                        options.Command = "--help";
                    }
                    else if (argument.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new CliException(L.T("option.unknown", argument));
                    }
                    else
                    {
                        options.Positionals.Add(argument);
                    }
                }
                return options;
            }

            private static string ReadOptionValue(string[] args, ref int index, string option)
            {
                if (index + 1 >= args.Length)
                {
                    throw new CliException(L.T("option.value_required", option));
                }
                index++;
                return args[index];
            }
        }
    }
}
