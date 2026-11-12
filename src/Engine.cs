// FreeFlow for Windows - local speech engine (whisper.cpp) that runs on the graphics card.
// NVIDIA cards use the CUDA build, every other PC uses the CPU build with a smaller model.
// The engine runs as a hidden whisper-server.exe on 127.0.0.1 and keeps the model loaded,
// so each clip takes well under a second on a modern GPU.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FreeFlowWin
{
    static class LocalEngine
    {
        const string WhisperTag = "v1.9.2";
        const string ReleaseBase = "https://github.com/ggml-org/whisper.cpp/releases/download/" + WhisperTag + "/";
        const string ModelBase = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeFlowWin");

        // ---- hardware
        static bool? nvidia;
        public static bool HasNvidia
        {
            get
            {
                if (nvidia == null)
                    nvidia = File.Exists(Path.Combine(Environment.SystemDirectory, "nvcuda.dll"));
                return nvidia.Value;
            }
        }

        static string gpuName;
        public static string GpuName
        {
            get
            {
                if (gpuName != null) return gpuName;
                gpuName = "";
                try
                {
                    // display adapters live under this class key; pick the NVIDIA one if there is one
                    using (var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                        if (cls != null)
                            foreach (var sub in cls.GetSubKeyNames())
                            {
                                try
                                {
                                    using (var k = cls.OpenSubKey(sub))
                                    {
                                        var d = k == null ? null : k.GetValue("DriverDesc") as string;
                                        if (string.IsNullOrEmpty(d)) continue;
                                        if (gpuName == "" || d.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) gpuName = d;
                                    }
                                }
                                catch { }
                            }
                }
                catch { }
                return gpuName;
            }
        }

        public static string Flavor { get { return HasNvidia ? "cuda" : "cpu"; } }
        public static string PlaceName { get { return HasNvidia ? "GPU" : "this PC"; } }
        static string ZipName { get { return HasNvidia ? "whisper-cublas-12.4.0-bin-x64.zip" : "whisper-bin-x64.zip"; } }
        public static string ModelFile { get { return HasNvidia ? "ggml-large-v3-turbo-q5_0.bin" : "ggml-small-q5_1.bin"; } }
        static long ModelMin { get { return HasNvidia ? 500000000L : 150000000L; } }
        public static string DownloadSize { get { return HasNvidia ? "1.2 GB" : "200 MB"; } }

        static string EngineDir { get { return Path.Combine(Root, "engine-" + Flavor + "-" + WhisperTag); } }
        static string ServerExe { get { return Path.Combine(EngineDir, "whisper-server.exe"); } }
        static string ModelPath { get { return Path.Combine(Root, "models", ModelFile); } }

        public static bool Installed
        {
            get
            {
                try { return File.Exists(ServerExe) && File.Exists(ModelPath) && new FileInfo(ModelPath).Length > ModelMin; }
                catch { return false; }
            }
        }

        // ---- status for the settings window
        public static string Status = "";
        public static float Progress = -1; // 0..1 while downloading, -1 otherwise
        public static bool Installing;
        public static event Action Changed;
        static SynchronizationContext ui;
        public static void Init() { ui = SynchronizationContext.Current; }
        static void Fire()
        {
            var c = Changed;
            if (c == null) return;
            if (ui != null) ui.Post(delegate { c(); }, null); else c();
        }
        static void Set(string status, float progress) { Status = status; Progress = progress; Fire(); }

        // ---- install (download engine + model)
        public static async Task<bool> Install()
        {
            if (Installing) return false;
            Installing = true;
            try
            {
                Directory.CreateDirectory(Path.Combine(Root, "models"));
                if (!File.Exists(ServerExe))
                {
                    string zip = Path.Combine(Root, "engine-download.zip");
                    await Download(ReleaseBase + ZipName, zip, "Downloading engine", 0f, HasNvidia ? 0.55f : 0.05f);
                    Set("Unpacking...", -1);
                    await Task.Run(delegate { Unpack(zip, EngineDir); });
                    try { File.Delete(zip); } catch { }
                }
                if (!File.Exists(ModelPath) || new FileInfo(ModelPath).Length < ModelMin)
                {
                    string part = ModelPath + ".part";
                    await Download(ModelBase + ModelFile, part, "Downloading voice model", HasNvidia ? 0.55f : 0.05f, 1f);
                    if (File.Exists(ModelPath)) File.Delete(ModelPath);
                    File.Move(part, ModelPath);
                }
                Set("Starting...", -1);
                return await Start();
            }
            catch (Exception ex)
            {
                Set("Download failed: " + Short(ex.Message), -1);
                return false;
            }
            finally { Installing = false; Fire(); }
        }

        static async Task Download(string url, string path, string label, float from, float to)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromHours(2) })
            using (var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                res.EnsureSuccessStatusCode();
                long total = res.Content.Headers.ContentLength ?? 0;
                using (var src = await res.Content.ReadAsStreamAsync())
                using (var dst = File.Create(path))
                {
                    var buf = new byte[1 << 16];
                    long got = 0; int n; var last = DateTime.MinValue;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, n);
                        got += n;
                        if ((DateTime.Now - last).TotalMilliseconds > 250)
                        {
                            last = DateTime.Now;
                            float f = total > 0 ? (float)got / total : 0;
                            Set(label + "  " + (got >> 20) + " / " + (total >> 20) + " MB", from + (to - from) * f);
                        }
                    }
                }
            }
        }

        // only the server and its DLLs are needed (CUDA zip has many demo tools)
        static void Unpack(string zip, string dir)
        {
            Directory.CreateDirectory(dir);
            using (var z = ZipFile.OpenRead(zip))
                foreach (var e in z.Entries)
                {
                    string n = e.Name.ToLowerInvariant();
                    if (n == "whisper-server.exe" || n.EndsWith(".dll"))
                        e.ExtractToFile(Path.Combine(dir, e.Name), true);
                }
            if (!File.Exists(Path.Combine(dir, "whisper-server.exe"))) throw new Exception("engine zip has no whisper-server.exe");
        }

        // ---- server
        static Process proc;
        static int port;
        static Task<bool> starting;
        static DateTime lastUse = DateTime.Now;
        static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        static IntPtr job;

        public static bool Running { get { return proc != null && !proc.HasExited && port > 0 && starting != null && starting.IsCompleted && starting.Result; } }

        public static Task<bool> Start()
        {
            if (starting != null && !starting.IsCompleted) return starting;
            if (proc != null && !proc.HasExited && starting != null && starting.Result) return starting;
            starting = StartCore();
            return starting;
        }

        static async Task<bool> StartCore()
        {
            if (!Installed) return false;
            Stop();
            KillStrays();
            port = FreePort();
            int threads = Math.Max(2, Math.Min(8, Environment.ProcessorCount / 2));
            var psi = new ProcessStartInfo(ServerExe,
                "-m \"" + ModelPath + "\" --host 127.0.0.1 --port " + port + " -l auto -nt -t " + threads)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = EngineDir,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            Set("Loading model...", -1);
            try
            {
                proc = Process.Start(psi);
                proc.OutputDataReceived += delegate { }; proc.ErrorDataReceived += delegate { };
                proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
                TieToUs(proc);
            }
            catch (Exception ex) { Set("Could not start: " + Short(ex.Message), -1); return false; }

            for (int i = 0; i < 120; i++)
            {
                if (proc.HasExited) { Set("Engine stopped (code " + proc.ExitCode + ")", -1); return false; }
                try
                {
                    var r = await http.GetAsync("http://127.0.0.1:" + port + "/");
                    if (r.IsSuccessStatusCode) break;
                }
                catch { }
                await Task.Delay(250);
            }
            // warm up so the first real clip is fast
            try { await Post(Silence(), "", ""); } catch { }
            lastUse = DateTime.Now;
            Set("Ready", -1);
            return true;
        }

        public static void Stop()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            proc = null; port = 0;
            if (!Installing) Set(Installed ? "Sleeping (loads when you talk)" : "", -1);
        }

        // free memory when not used for a while; it loads again (about 2 s) the moment the talk key goes down
        public static void UnloadIfIdle(int minutes)
        {
            if (proc != null && !proc.HasExited && (DateTime.Now - lastUse).TotalMinutes > minutes && starting != null && starting.IsCompleted)
                Stop();
        }

        static void KillStrays()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("whisper-server"))
                {
                    try { if (p.MainModule.FileName.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) p.Kill(); } catch { }
                    p.Dispose();
                }
            }
            catch { }
        }

        static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start(); int p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
            return p;
        }

        public static async Task<string> Transcribe(byte[] wav, string language, string prompt)
        {
            if (!Running)
            {
                bool ok = await Start();
                if (!ok) throw new Exception("Local engine not ready: " + Status);
            }
            lastUse = DateTime.Now;
            string text = await Post(wav, language, prompt);
            lastUse = DateTime.Now;
            return text;
        }

        static async Task<string> Post(byte[] wav, string language, string prompt)
        {
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(wav);
            file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(file, "file", "audio.wav");
            form.Add(new StringContent("json"), "response_format");
            form.Add(new StringContent("0.0"), "temperature");
            form.Add(new StringContent(string.IsNullOrEmpty(language) ? "auto" : language), "language");
            if (!string.IsNullOrEmpty(prompt)) form.Add(new StringContent(prompt.Length > 600 ? prompt.Substring(0, 600) : prompt), "prompt");
            var res = await http.PostAsync("http://127.0.0.1:" + port + "/inference", form);
            string body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new Exception("Local engine error (" + (int)res.StatusCode + ")");
            var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
            string t = d.ContainsKey("text") ? (d["text"] as string ?? "") : "";
            // the server puts each segment on its own line
            return string.Join(" ", t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Replace("  ", " ").Trim();
        }

        static byte[] Silence()
        {
            int n = 16000; // 1 s
            var ms = new MemoryStream(); var w = new BinaryWriter(ms);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2); w.Write(new byte[n * 2]);
            return ms.ToArray();
        }

        static string Short(string s) { return s.Length > 80 ? s.Substring(0, 80) : s; }

        // the server dies with FreeFlow, even if FreeFlow crashes (Windows job object, kill-on-close)
        static void TieToUs(Process p)
        {
            try
            {
                if (job == IntPtr.Zero)
                {
                    job = CreateJobObject(IntPtr.Zero, null);
                    var info = new byte[144]; // JOBOBJECT_EXTENDED_LIMIT_INFORMATION (x64)
                    BitConverter.GetBytes(0x2000).CopyTo(info, 16); // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    var h = GCHandle.Alloc(info, GCHandleType.Pinned);
                    try { SetInformationJobObject(job, 9, h.AddrOfPinnedObject(), (uint)info.Length); } finally { h.Free(); }
                }
                AssignProcessToJobObject(job, p.Handle);
            }
            catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attr, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, uint len);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);
    }
}
