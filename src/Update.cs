// FreeFlow for Windows - auto update from GitHub Releases.
// Checks github.com/<repo>/releases/latest (no API key, no rate limit), downloads FreeFlow.exe,
// swaps it in next to the running exe and restarts when the app is idle.

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FreeFlowWin
{
    static class Updater
    {
        public static string Version { get { return BuildInfo.Version; } }
        public static string Repo { get { return BuildInfo.Repo; } }
        public static string Status = "";
        public static string ReadyTag;   // downloaded and waiting to be applied
        public static event Action Changed;

        static string Exe { get { return Application.ExecutablePath; } }
        static string NewExe { get { return Exe + ".new"; } }
        static string OldExe { get { return Exe + ".old"; } }

        public static void CleanupOld()
        {
            for (int i = 0; i < 10 && File.Exists(OldExe); i++)
            {
                try { File.Delete(OldExe); } catch { System.Threading.Thread.Sleep(300); }
            }
        }

        static void Fire() { var c = Changed; if (c != null) c(); }

        // "v3.1.0" -> 3.1.0 ; returns null if it is not a version
        static System.Version Parse(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            System.Version v;
            return System.Version.TryParse(tag.TrimStart('v', 'V'), out v) ? v : null;
        }

        public static async Task<string> LatestTag()
        {
            var h = new HttpClientHandler { AllowAutoRedirect = false };
            using (var c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(20) })
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("FreeFlowWin/" + Version);
                var r = await c.GetAsync("https://github.com/" + Repo + "/releases/latest");
                var loc = r.Headers.Location;
                if (loc == null) return null;
                string s = loc.ToString();
                int i = s.IndexOf("/tag/");
                return i < 0 ? null : Uri.UnescapeDataString(s.Substring(i + 5));
            }
        }

        // returns true when a newer version is downloaded and ready to apply
        public static async Task<bool> Check(bool manual)
        {
            if (string.IsNullOrEmpty(Repo) || Repo.Contains("OWNER")) { Status = "Updates not set up"; Fire(); return false; }
            if (ReadyTag != null) return true;
            try
            {
                Status = "Checking..."; Fire();
                string tag = await LatestTag();
                var latest = Parse(tag); var mine = Parse(Version);
                if (latest == null || mine == null || latest <= mine) { Status = "Up to date"; Fire(); return false; }
                Status = "Downloading " + tag + "..."; Fire();
                using (var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    c.DefaultRequestHeaders.UserAgent.ParseAdd("FreeFlowWin/" + Version);
                    var bytes = await c.GetByteArrayAsync("https://github.com/" + Repo + "/releases/download/" + tag + "/FreeFlow.exe");
                    if (bytes.Length < 50000 || bytes[0] != 'M' || bytes[1] != 'Z') throw new Exception("bad download");
                    File.WriteAllBytes(NewExe, bytes);
                }
                ReadyTag = tag;
                Status = tag + " ready, installs when idle"; Fire();
                return true;
            }
            catch (UnauthorizedAccessException) { Status = "Can't write to the app folder"; Fire(); return false; }
            catch (Exception ex) { Status = manual ? "Check failed: " + ex.Message : "Up to date"; Fire(); return false; }
        }

        // swap exe files and start the new one. Caller quits the app right after.
        public static bool Apply()
        {
            if (ReadyTag == null || !File.Exists(NewExe)) return false;
            try
            {
                if (File.Exists(OldExe)) File.Delete(OldExe);
                File.Move(Exe, OldExe);          // a running exe can be renamed, not overwritten
                File.Move(NewExe, Exe);
                Process.Start(Exe, "--updated " + ReadyTag);
                return true;
            }
            catch
            {
                try { if (!File.Exists(Exe) && File.Exists(OldExe)) File.Move(OldExe, Exe); } catch { }
                ReadyTag = null; Status = "Update failed"; Fire();
                return false;
            }
        }
    }
}
