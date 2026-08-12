using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

const string distro = "Ubuntu";
const string image = @"C:\ChromeOSLab\flex\chromeos-flex-compressed.qcow2";
const string viewer = @"C:\ChromeOSLab\tools\vncviewer.exe";
const int vncPort = 5901;

try
{
    CheckFiles();
    await StopOldSession();
    StartQemu();
    await WaitForVnc();
    await SelectTablet();
    ShowViewer();
}
catch (Exception error)
{
    MessageBox.Show(error.Message, "ChromeOS Emulator", MessageBoxButtons.OK, MessageBoxIcon.Error);
}

void CheckFiles()
{
    if (!File.Exists(image))
        throw new FileNotFoundException("ChromeOS disk not found.", image);

    if (!File.Exists(viewer))
        throw new FileNotFoundException("VNC viewer not found.", viewer);
}
async Task StopOldSession()
{
    foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(viewer)))
    {
        try { process.Kill(entireProcessTree: true); }
        catch { }
    }
    await Run("wsl.exe", "-d", distro, "-u", "root", "--",
        "pkill", "-TERM", "-f", "[q]emu-system-x86_64");
}
void StartQemu()
{
    const string command =
        "IMAGE=/mnt/c/ChromeOSLab/flex/chromeos-flex-compressed.qcow2 " +
        "MEMORY=4G CORES=6 exec \"$HOME/chromeos-lab/run.sh\"";
    Start("wsl.exe", ["-d", distro, "--", "bash", "-lc", command], hidden: true).Dispose();
}
async Task WaitForVnc()
{
    var deadline = DateTime.UtcNow.AddSeconds(90);
    var successfulChecks = 0;

    while (DateTime.UtcNow < deadline)
    {
        try
        {
            using var connection = new TcpClient();
            await connection.ConnectAsync(IPAddress.Loopback, vncPort);
            successfulChecks++;

            if (successfulChecks == 5)
                return;
        }
        catch (SocketException)
        {
            successfulChecks = 0;
        }

        await Task.Delay(200);
    }

    throw new TimeoutException("ChromeOS did not start. Check ~/chromeos-lab/qemu.log in Ubuntu.");
}

Task SelectTablet() => Run("wsl.exe", "-d", distro, "--", "bash", "-lc",
    "printf 'mouse_set 4\\n' | socat - UNIX-CONNECT:\"$HOME/chromeos-lab/qemu-monitor.sock\"");

void ShowViewer()
{
    var process = Start(viewer,
    [
        "-Shared", "-SecurityTypes", "None",
        "-AlwaysCursor=1", "-CursorType=System",
        "-AutoSelect=0", "-FullColor=1",
        "-PreferredEncoding=ZRLE", "-NoJpeg=1",
        "-PointerEventInterval=8", $"127.0.0.1::{vncPort}"
    ]);

    process.WaitForInputIdle(10_000);
    process.Refresh();

    if (process.MainWindowHandle == IntPtr.Zero)
        return;

    var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
    var width = Math.Min(1296, screen.Width);
    var height = Math.Min(839, screen.Height);
    var x = screen.Left + (screen.Width - width) / 2;
    var y = screen.Top + (screen.Height - height) / 2;

    ShowWindow(process.MainWindowHandle, 9);
    SetWindowPos(process.MainWindowHandle, IntPtr.Zero, x, y, width, height, 0);
    SetForegroundWindow(process.MainWindowHandle);
}

async Task<int> Run(string file, params string[] args)
{
    using var process = Start(file, args, hidden: true);
    await process.WaitForExitAsync();
    return process.ExitCode;
}

Process Start(string file, IEnumerable<string> args, bool hidden = false)
{
    var info = new ProcessStartInfo(file)
    {
        UseShellExecute = false,
        CreateNoWindow = hidden,
        WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
    };

    foreach (var arg in args)
        info.ArgumentList.Add(arg);

    return Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
}

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool ShowWindow(IntPtr window, int command);

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool SetForegroundWindow(IntPtr window);

[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool SetWindowPos(
    IntPtr window, IntPtr insertAfter,
    int x, int y, int width, int height, uint flags);
