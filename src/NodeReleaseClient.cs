using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Nover
{
    internal sealed class NodeReleaseClient
    {
        private readonly AppConfig _config;
        private List<NodeRelease> _cachedReleases;

        public NodeReleaseClient(AppConfig config)
        {
            _config = config;
        }

        public NodeRelease ResolveRemote(string specification)
        {
            if (String.IsNullOrWhiteSpace(specification))
            {
                throw new CliException(L.T("install.version_required"));
            }

            string requested = specification.Trim();
            bool latest = String.Equals(
                requested,
                "latest",
                StringComparison.OrdinalIgnoreCase);
            bool lts = String.Equals(
                requested,
                "lts",
                StringComparison.OrdinalIgnoreCase);
            VersionConstraint constraint = latest || lts
                ? null
                : VersionConstraint.Parse(requested);

            // 先校验语法，避免无效输入触发网络请求。
            List<NodeRelease> releases = GetReleases();
            if (latest)
            {
                return releases[0];
            }
            if (lts)
            {
                foreach (NodeRelease release in releases)
                {
                    if (release.IsLts)
                    {
                        return release;
                    }
                }
                throw new CliException(L.T("install.no_lts"));
            }

            foreach (NodeRelease release in releases)
            {
                if (constraint.Matches(release.Version))
                {
                    return release;
                }
            }
            throw new CliException(L.T("install.no_remote_match", requested));
        }

        public NodeVersion ResolveInstalled(VersionConstraint constraint)
        {
            List<NodeVersion> installed = GetInstalled();
            if (installed.Count == 0)
            {
                throw new CliException(L.T("install.no_versions"));
            }

            foreach (NodeVersion version in installed)
            {
                if (constraint.Matches(version))
                {
                    return version;
                }
            }

            throw new CliException(L.T("install.no_installed_match", constraint.Text));
        }

        public List<NodeRelease> GetReleases()
        {
            if (_cachedReleases != null)
            {
                return _cachedReleases;
            }

            string indexUrl = AppConfig.CombineUrl(_config.Mirror, "index.json");
            string json = DownloadText(
                indexUrl,
                L.T("install.index_label"));
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            object parsed;
            try
            {
                parsed = serializer.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                throw new CliException(L.T("install.index_parse", ex.Message));
            }

            object[] rows = parsed as object[];
            if (rows == null)
            {
                throw new CliException(L.T("install.index_format"));
            }

            List<NodeRelease> releases = new List<NodeRelease>();
            // 跳过坏行，不因单条记录丢弃整个索引。
            foreach (object rowObject in rows)
            {
                Dictionary<string, object> row = rowObject as Dictionary<string, object>;
                if (row == null || !row.ContainsKey("version"))
                {
                    continue;
                }

                NodeVersion version;
                int components;
                if (!NodeVersion.TryParse(Convert.ToString(row["version"]), out version, out components))
                {
                    continue;
                }

                NodeRelease release = new NodeRelease();
                release.Version = version;
                release.IsLts = IsLtsValue(row.ContainsKey("lts") ? row["lts"] : null);
                object[] files = row.ContainsKey("files") ? row["files"] as object[] : null;
                if (files != null)
                {
                    foreach (object file in files)
                    {
                        string name = Convert.ToString(file);
                        if (!String.IsNullOrWhiteSpace(name))
                        {
                            release.Files.Add(name);
                        }
                    }
                }
                releases.Add(release);
            }

            if (releases.Count == 0)
            {
                throw new CliException(L.T("install.index_empty"));
            }
            releases.Sort(delegate(NodeRelease left, NodeRelease right)
            {
                // 统一使用降序，后续直接取第一个匹配项。
                return right.Version.CompareTo(left.Version);
            });
            _cachedReleases = releases;
            return releases;
        }

        public List<NodeVersion> GetInstalled()
        {
            List<NodeVersion> versions = new List<NodeVersion>();
            if (!Directory.Exists(_config.VersionsPath))
            {
                return versions;
            }

            foreach (string directory in Directory.GetDirectories(_config.VersionsPath, "v*"))
            {
                // 三段目录且存在 node.exe 才算完整安装。
                NodeVersion version;
                int components;
                if (NodeVersion.TryParse(Path.GetFileName(directory), out version, out components)
                    && components == 3
                    && File.Exists(Path.Combine(directory, "node.exe")))
                {
                    versions.Add(version);
                }
            }
            versions.Sort(delegate(NodeVersion left, NodeVersion right)
            {
                return right.CompareTo(left);
            });
            return versions;
        }

        public void DownloadArchive(NodeRelease release, string architecture, string destination)
        {
            if (!release.HasArchive(architecture))
            {
                throw new CliException(
                    L.T("install.no_archive", release.Version, architecture));
            }

            string baseUrl = _config.ReleaseBaseUrl(release.Version);
            string archiveName = _config.ReleaseArchiveName(release.Version, architecture);
            string archiveUrl = AppConfig.CombineUrl(baseUrl, archiveName);
            string sumsUrl = AppConfig.CombineUrl(baseUrl, "SHASUMS256.txt");

            Console.WriteLine(L.T("install.downloading") + " " + archiveUrl);
            DownloadFile(archiveUrl, destination);
            Console.WriteLine(L.T("install.verifying"));
            string expected = ReadExpectedChecksum(sumsUrl, archiveName);
            string actual = ComputeSha256(destination);
            if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                throw new CliException(
                    L.T("install.checksum_mismatch", archiveName, expected, actual));
            }
        }

        private string ReadExpectedChecksum(string sumsUrl, string archiveName)
        {
            // 校验文件格式：哈希、空白、文件名。
            string sums = DownloadText(sumsUrl, "SHASUMS256.txt");
            using (StringReader reader = new StringReader(sums))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    int separator = trimmed.IndexOfAny(new char[] { ' ', '\t' });
                    if (separator <= 0)
                    {
                        continue;
                    }
                    string hash = trimmed.Substring(0, separator).Trim();
                    string name = trimmed.Substring(separator).Trim();
                    if (name.StartsWith("*", StringComparison.Ordinal))
                    {
                        name = name.Substring(1);
                    }
                    if (String.Equals(name, archiveName, StringComparison.OrdinalIgnoreCase))
                    {
                        return hash;
                    }
                }
            }
            throw new CliException(L.T("install.checksum_missing", archiveName));
        }

        private string DownloadText(string url, string label)
        {
            try
            {
                using (TimedWebClient client = CreateClient())
                {
                    return client.DownloadString(url);
                }
            }
            catch (Exception ex)
            {
                throw new CliException(
                    L.T("install.download_text_error", label, url, ex.Message));
            }
        }

        private void DownloadFile(string url, string destination)
        {
            try
            {
                using (TimedWebClient client = CreateClient())
                {
                    client.DownloadFile(url, destination);
                }
            }
            catch (Exception ex)
            {
                throw new CliException(L.T("install.download_error", url, ex.Message));
            }
        }

        private static TimedWebClient CreateClient()
        {
            // Node.js 站点要求 TLS 1.2；下载超时单独放宽。
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            TimedWebClient client = new TimedWebClient();
            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.UserAgent] = "nover/" + Program.ToolVersion;
            return client;
        }

        private static bool IsLtsValue(object value)
        {
            if (value == null)
            {
                return false;
            }
            if (value is bool)
            {
                return (bool)value;
            }
            string text = Convert.ToString(value);
            return !String.IsNullOrWhiteSpace(text)
                && !String.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private sealed class TimedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                HttpWebRequest http = request as HttpWebRequest;
                if (http != null)
                {
                    http.Timeout = 60000;
                    http.ReadWriteTimeout = 600000;
                }
                return request;
            }
        }
    }

    internal sealed class GlobalVersionStore
    {
        private readonly AppConfig _config;
        private readonly NodeReleaseClient _releases;

        public string StatePath
        {
            get { return Path.Combine(_config.Home, "active.txt"); }
        }

        public GlobalVersionStore(AppConfig config, NodeReleaseClient releases)
        {
            _config = config;
            _releases = releases;
        }

        public NodeVersion Read()
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }

            string value = File.ReadAllText(StatePath).Trim();
            NodeVersion version;
            int components;
            if (!NodeVersion.TryParse(value, out version, out components) || components != 3)
            {
                throw new CliException(L.T("state.invalid", StatePath));
            }
            if (!File.Exists(Path.Combine(_config.VersionPath(version), "node.exe")))
            {
                throw new CliException(L.T("state.incomplete", version));
            }
            return version;
        }

        public NodeVersion Use(string specification)
        {
            NodeVersion version = _releases.ResolveInstalled(VersionConstraint.Parse(specification));
            Write(version);
            return version;
        }

        public void Write(NodeVersion version)
        {
            string versionPath = _config.VersionPath(version);
            if (!File.Exists(Path.Combine(versionPath, "node.exe")))
            {
                throw new CliException(L.T("install.version_incomplete", version));
            }

            Directory.CreateDirectory(_config.Home);
            // 临时文件写完后再替换，避免留下半个 active.txt。
            string temporary = StatePath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, version.ToString() + Environment.NewLine);
            try
            {
                if (File.Exists(StatePath))
                {
                    try
                    {
                        File.Replace(temporary, StatePath, null);
                    }
                    catch
                    {
                        File.Copy(temporary, StatePath, true);
                        File.Delete(temporary);
                    }
                }
                else
                {
                    File.Move(temporary, StatePath);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }

    internal sealed class Installer
    {
        private readonly AppConfig _config;
        private readonly NodeReleaseClient _releases;

        public Installer(AppConfig config, NodeReleaseClient releases)
        {
            _config = config;
            _releases = releases;
        }

        public NodeVersion Install(string specification, string architecture)
        {
            _config.EnsureDirectories();
            NodeRelease release = _releases.ResolveRemote(specification);
            string finalPath = _config.VersionPath(release.Version);
            if (Safety.PathExists(finalPath))
            {
                Console.WriteLine(L.T("install.already", release.Version));
                return release.Version;
            }

            string temporaryRoot = Path.Combine(
                _config.Home,
                ".install-" + Guid.NewGuid().ToString("N"));
            string archivePath = Path.Combine(
                temporaryRoot,
                _config.ReleaseArchiveName(release.Version, architecture));
            string extractPath = Path.Combine(temporaryRoot, "extract");

            try
            {
                // 下载、校验、解压都在暂存目录完成后再发布。
                Directory.CreateDirectory(extractPath);
                _releases.DownloadArchive(release, architecture, archivePath);
                ValidateArchivePathBudget(archivePath, extractPath, finalPath);

                Console.WriteLine(L.T("install.extracting"));
                ExtractArchive(archivePath, extractPath);
                string nodeRoot = FindExtractedNodeRoot(extractPath);
                if (!File.Exists(Path.Combine(nodeRoot, "node.exe")))
                {
                    throw new CliException(L.T("install.archive_missing_node"));
                }

                Directory.CreateDirectory(_config.VersionsPath);
                Directory.Move(nodeRoot, finalPath);
                Console.WriteLine(L.T("install.done", release.Version, architecture));
                return release.Version;
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryRoot);
            }
        }

        private static void ValidateArchivePathBudget(
            string archivePath,
            string extractPath,
            string finalPath)
        {
            // 解压前预估最长路径，避免超过 Windows 限制。
            int longestEntry = 0;
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    longestEntry = Math.Max(longestEntry, entry.FullName.Length);
                }
            }

            const int windowsPathLimit = 260;
            if (extractPath.Length + longestEntry + 1 >= windowsPathLimit
                || finalPath.Length + longestEntry + 1 >= windowsPathLimit)
            {
                throw new CliException(L.T("install.path_too_deep"));
            }
        }

        private static void ExtractArchive(string archivePath, string destination)
        {
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    // 原始路径和规范化目标都要校验。
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    ValidateArchiveEntry(relative);
                    string target = Path.GetFullPath(Path.Combine(destination, relative));
                    Safety.EnsureChildPath(destination, target);

                    if (String.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    string parent = Path.GetDirectoryName(target);
                    if (!String.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }
                    entry.ExtractToFile(target, true);
                }
            }
        }

        private static void ValidateArchiveEntry(string relative)
        {
            if (String.IsNullOrWhiteSpace(relative)
                || Path.IsPathRooted(relative)
                || relative.IndexOf(':') >= 0)
            {
                throw new CliException(L.T("archive.unsafe_path", relative));
            }

            string[] segments = relative.Split(
                new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                if (segment == ".." || segment == ".")
                {
                    throw new CliException(L.T("archive.path_traversal", relative));
                }
            }
        }

        private static string FindExtractedNodeRoot(string extractPath)
        {
            // 官方 ZIP 通常只有一个顶层 Node.js 目录。
            if (File.Exists(Path.Combine(extractPath, "node.exe")))
            {
                return extractPath;
            }

            string selected = null;
            foreach (string candidate in Directory.GetDirectories(extractPath))
            {
                if (File.Exists(Path.Combine(candidate, "node.exe")))
                {
                    if (selected != null)
                    {
                        throw new CliException(L.T("archive.multiple_roots"));
                    }
                    selected = candidate;
                }
            }
            if (selected == null)
            {
                throw new CliException(L.T("archive.root_not_found"));
            }
            return selected;
        }

        private void DeleteTemporaryDirectory(string path)
        {
            if (!Safety.PathExists(path))
            {
                return;
            }
            string safePath = Safety.EnsureChildPath(_config.Home, path);
            if (!Safety.IsReparsePoint(safePath))
            {
                // 不递归清理重解析点。
                Directory.Delete(safePath, true);
            }
        }
    }
}
