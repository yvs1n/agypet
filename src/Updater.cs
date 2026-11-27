using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
#if NET8_0_OR_GREATER
using System.Net.Http;
using System.Runtime.InteropServices;
#else
using System.Windows.Threading;
#endif

namespace AgyPet
{
    // Keeps AgyPet up to date from GitHub Releases (https://github.com/yvs1n/agypet/releases).
    // Downloads and verifies the latest OS release binary in the background.
    public class Updater
    {
        public const string Repo = "yvs1n/agypet";
        const string LatestUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
        const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";
        const long MaxDownloadBytes = 140 * 1024 * 1024;
        static readonly TimeSpan FirstCheckAfter = TimeSpan.FromSeconds(15);
        static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);

        public static readonly string AssetName = ResolveAssetName();

        public static string ResolveAssetName()
        {
#if NET8_0_OR_GREATER
            if (OperatingSystem.IsMacOS()) return "AgyPet-macOS.dmg";
            if (OperatingSystem.IsLinux())
                return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "AgyPet-linux-arm64" : "AgyPet-linux-x64";
#endif
            return "AgyPet.exe";
        }

        public class Release
        {
            public string Tag, Url, Sha256, HtmlUrl;
        }

        public static readonly Version Current = Assembly.GetExecutingAssembly().GetName().Version;
        static string UpdateFolder { get { return Path.Combine(PetSettings.DataDir, "update"); } }

        readonly PetSettings settings;
#if NET8_0_OR_GREATER
        readonly Action<Action> uiInvoke;
        readonly Action shutdownApp;
        readonly System.Timers.Timer timer = new System.Timers.Timer(60000) { AutoReset = true };
#else
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        readonly Dispatcher ui = Dispatcher.CurrentDispatcher;
#endif

        DateTime nextCheck = DateTime.Now + FirstCheckAfter;
        string readyExe, readyTag;
        bool checking, manualRequested;

        public string Status { get; private set; }
        public bool UpdateReady { get { return !string.IsNullOrEmpty(readyExe); } }
        public string ReadyTag { get { return readyTag ?? ""; } }
        public event Action Changed;

#if NET8_0_OR_GREATER
        public Updater(PetSettings settings, Action<Action> uiInvoke = null, Action shutdownApp = null)
        {
            this.settings = settings;
            this.uiInvoke = uiInvoke ?? (a => a());
            this.shutdownApp = shutdownApp;
            Status = "v" + Short(Current);
            timer.Elapsed += (s, e) => Tick();
        }
#else
        public Updater(PetSettings settings)
        {
            this.settings = settings;
            Status = "v" + Short(Current);
            timer.Tick += (s, e) => Tick();
        }
#endif

        public static string Short(Version v)
        {
            return v.Major + "." + v.Minor + "." + Math.Max(0, v.Build);
        }

        public void Start()
        {
            CleanUpOldUpdateFiles();
            timer.Start();
#if NET8_0_OR_GREATER
            var boot = new System.Timers.Timer(FirstCheckAfter.TotalMilliseconds) { AutoReset = false };
            boot.Elapsed += (s, e) =>
            {
                boot.Dispose();
                uiInvoke(() => { if (settings.AutoUpdate) Check(manual: false); });
            };
            boot.Start();
#else
            var boot = new DispatcherTimer { Interval = FirstCheckAfter };
            boot.Tick += (s, e) =>
            {
                boot.Stop();
                if (settings.AutoUpdate) Check(manual: false);
            };
            boot.Start();
#endif
        }

        public void CheckNow()
        {
            if (UpdateReady)
            {
                ApplyReadyUpdate();
                return;
            }
            Check(manual: true);
        }

        void Tick()
        {
            if (!checking && settings.AutoUpdate && DateTime.Now >= nextCheck && !UpdateReady)
                Check(manual: false);
        }

        void Check(bool manual)
        {
            if (checking) return;
            checking = true;
            manualRequested = manual;
            nextCheck = DateTime.Now + CheckEvery;
            SetStatus("Checking for updates...");

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string msg = null, exe = null, tag = null;
                try
                {
                    var rel = ParseRelease(Fetch(LatestUrl));
                    if (rel == null || !IsNewer(rel.Tag, Current))
                    {
                        msg = "Up to date (v" + Short(Current) + ")";
                    }
                    else
                    {
                        exe = Download(rel);
                        tag = rel.Tag;
                    }
                }
                catch
                {
                    msg = manual ? "Update check failed (v" + Short(Current) + ")" : "v" + Short(Current);
                }

                Action finish = () =>
                {
                    checking = false;
                    if (exe != null)
                    {
                        readyExe = exe;
                        readyTag = tag;
                        SetStatus("Update " + tag + " ready");
                        if (manualRequested) ApplyReadyUpdate();
                    }
                    else
                    {
                        SetStatus(msg ?? ("v" + Short(Current)));
                    }
                    manualRequested = false;
                };
#if NET8_0_OR_GREATER
                uiInvoke(finish);
#else
                ui.BeginInvoke(finish);
#endif
            });
        }

        public void ApplyReadyUpdate()
        {
            if (string.IsNullOrEmpty(readyExe) || !File.Exists(readyExe)) return;
            string currentExe = EnvironmentProcessPath();
            string exe = readyExe;
            readyExe = null;
            try
            {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsMacOS())
                {
                    SetStatus("Opening " + readyTag + " installer...");
                    Process.Start(new ProcessStartInfo("open", "\"" + exe + "\"") { UseShellExecute = false });
                    return;
                }
                if (OperatingSystem.IsLinux())
                {
                    try { File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
                    catch { }
                }
#endif
                SetStatus("Installing " + readyTag + "...");
                Process.Start(new ProcessStartInfo(exe, "--update \"" + currentExe + "\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(currentExe) ?? PetSettings.DataDir
                });
#if NET8_0_OR_GREATER
                if (shutdownApp != null) shutdownApp();
#else
                System.Windows.Application.Current.Shutdown();
#endif
            }
            catch
            {
                SetStatus("Couldn't start update");
            }
        }

        static string EnvironmentProcessPath()
        {
#if NET8_0_OR_GREATER
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule.FileName;
#else
            return Process.GetCurrentProcess().MainModule.FileName;
#endif
        }

        // Executed when launched as: AgyPet --update "<targetPath>"
        public static void RunSelfUpdate(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return;
            string self = EnvironmentProcessPath();
            if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
                return;

            // Wait up to ~8s for the previous AgyPet process to release file lock
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    string tmp = targetPath + ".new";
                    File.Copy(self, tmp, true);
#if NET8_0_OR_GREATER
                    if (OperatingSystem.IsLinux())
                    {
                        try { File.SetUnixFileMode(tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
                        catch { }
                    }
#endif
                    if (File.Exists(targetPath)) File.Replace(tmp, targetPath, null);
                    else File.Move(tmp, targetPath);

                    Process.Start(new ProcessStartInfo(targetPath)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = dir ?? PetSettings.DataDir
                    });
                    return;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
        }

        static void CleanUpOldUpdateFiles()
        {
            try
            {
                string currentExe = EnvironmentProcessPath();
                string staged = Path.Combine(UpdateFolder, AssetName);
                if (File.Exists(staged) && !string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(staged), StringComparison.OrdinalIgnoreCase))
                    File.Delete(staged);
            }
            catch { }
        }

        void SetStatus(string s)
        {
            Status = s;
            var handler = Changed;
            if (handler != null) handler();
        }

        // ---- Release Parsing & Verification ----

        public static Release ParseRelease(string json, string assetOverride = null)
        {
            string wantedAsset = string.IsNullOrEmpty(assetOverride) ? AssetName : assetOverride;
            var d = Json.ParseObject(json);
            if (d == null) return null;
            string tag = Json.Str(d, "tag_name");
            if (string.IsNullOrEmpty(tag)) return null;

            object v;
            if (d.TryGetValue("draft", out v) && v is bool && (bool)v) return null;
            if (d.TryGetValue("prerelease", out v) && v is bool && (bool)v) return null;

            var assets = d.TryGetValue("assets", out v) ? v as IEnumerable : null;
            if (assets == null) return null;

            foreach (object o in assets)
            {
                var a = o as Dictionary<string, object>;
                if (a == null || !string.Equals(Json.Str(a, "name"), wantedAsset, StringComparison.OrdinalIgnoreCase)) continue;
                string url = Json.Str(a, "browser_download_url");
                if (!url.StartsWith(DownloadPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                string digest = Json.Str(a, "digest");
                return new Release
                {
                    Tag = tag,
                    Url = url,
                    HtmlUrl = Json.Str(d, "html_url"),
                    Sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest.Substring(7) : ""
                };
            }
            return null;
        }

        public static bool IsNewer(string tag, Version current)
        {
            Version v = VersionOf(tag);
            if (v == null) return false;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build)) > new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        }

        static Version VersionOf(string tag)
        {
            Version v;
            return Version.TryParse((tag ?? "").TrimStart('v', 'V'), out v) ? v : null;
        }

#if NET8_0_OR_GREATER
        static readonly HttpClient SharedHttp = CreateHttpClient();

        static HttpClient CreateHttpClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("AgyPet/" + Short(Current));
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        static string Fetch(string url)
        {
            return SharedHttp.GetStringAsync(url).GetAwaiter().GetResult();
        }

        static string Download(Release release)
        {
            Directory.CreateDirectory(UpdateFolder);
            string part = Path.Combine(UpdateFolder, AssetName + ".part");
            string exe = Path.Combine(UpdateFolder, AssetName);
            try
            {
                using (var resp = SharedHttp.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    resp.EnsureSuccessStatusCode();
                    if (resp.RequestMessage != null && resp.RequestMessage.RequestUri != null &&
                        resp.RequestMessage.RequestUri.Scheme != Uri.UriSchemeHttps)
                        throw new InvalidDataException("Download must use HTTPS");

                    using (var from = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var to = File.Create(part))
                    {
                        var buf = new byte[81920];
                        long total = 0;
                        int n;
                        while ((n = from.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += n;
                            if (total > MaxDownloadBytes) throw new InvalidDataException("Download exceeds limit");
                            to.Write(buf, 0, n);
                        }
                    }
                }
                Verify(part, release);
                if (File.Exists(exe)) File.Delete(exe);
                File.Move(part, exe);
                return exe;
            }
            finally { try { File.Delete(part); } catch { } }
        }
#else
        static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "AgyPet/" + Short(Current);
            r.Accept = "application/vnd.github+json, application/octet-stream";
            r.Timeout = 25000;
            r.ReadWriteTimeout = 60000;
            return r;
        }

        static string Fetch(string url)
        {
            using (var resp = Request(url).GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        static string Download(Release release)
        {
            Directory.CreateDirectory(UpdateFolder);
            string part = Path.Combine(UpdateFolder, AssetName + ".part");
            string exe = Path.Combine(UpdateFolder, AssetName);
            try
            {
                using (var resp = Request(release.Url).GetResponse())
                {
                    if (resp.ResponseUri.Scheme != Uri.UriSchemeHttps)
                        throw new InvalidDataException("Download must use HTTPS");
                    using (var from = resp.GetResponseStream())
                    using (var to = File.Create(part))
                    {
                        var buf = new byte[81920];
                        long total = 0;
                        int n;
                        while ((n = from.Read(buf, 0, buf.Length)) > 0)
                        {
                            total += n;
                            if (total > MaxDownloadBytes) throw new InvalidDataException("Download exceeds limit");
                            to.Write(buf, 0, n);
                        }
                    }
                }
                Verify(part, release);
                if (File.Exists(exe)) File.Delete(exe);
                File.Move(part, exe);
                return exe;
            }
            finally { try { File.Delete(part); } catch { } }
        }
#endif

        public static void Verify(string path, Release release)
        {
            if (!string.IsNullOrEmpty(release.Sha256))
            {
                if (!string.Equals(Sha256Of(path), release.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Checksum mismatch");
            }
#if !NET8_0_OR_GREATER
            AssemblyName name;
            try { name = AssemblyName.GetAssemblyName(path); }
            catch (Exception e) { throw new InvalidDataException("Invalid assembly: " + e.Message); }
            if (!string.Equals(name.Name, "AgyPet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected assembly name: " + name.Name);
            Version want = VersionOf(release.Tag);
            if (want == null || name.Version.Major != want.Major || name.Version.Minor != want.Minor || name.Version.Build != Math.Max(0, want.Build))
                throw new InvalidDataException("Version mismatch: " + name.Version + " vs " + release.Tag);
#else
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length < 1024 * 64)
                throw new InvalidDataException("Downloaded update file is incomplete");
#endif
        }

        internal static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "");
        }
    }
}
