using System.Diagnostics;

namespace GitPeach.Services;

public sealed class ShellService : IShellService
{
    public void OpenFolder(string path)
    {
        if (OperatingSystem.IsWindows()) Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else XdgOpen(path);
    }

    public void RevealInExplorer(string path)
    {
        // Linux file managers have no common "select this file" switch: open the folder that holds it.
        if (File.Exists(path) && OperatingSystem.IsWindows())
            Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else OpenFolder(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
    }

    public void OpenFile(string path)
    {
        if (OperatingSystem.IsWindows()) Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else XdgOpen(path);
    }

    public void OpenTerminal(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = directory, UseShellExecute = true });
            return;
        }
        // The first terminal that exists wins: the distribution's default, then the common desktops' ones.
        foreach (var term in new[] { "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm" })
        {
            try
            {
                Process.Start(new ProcessStartInfo(term) { WorkingDirectory = directory, UseShellExecute = false });
                return;
            }
            catch { /* not installed: try the next one */ }
        }
    }

    public bool OpenInTextPeach(string folder, string? file = null)
    {
        var exe = TextPeachLocator.FindOnThisMachine();
        if (exe is null) return false;
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = folder };
        psi.ArgumentList.Add(folder);
        if (file is not null) psi.ArgumentList.Add(file);
        Start(psi);
        return true;
    }

    public void OpenUrl(string url)
    {
        if (OperatingSystem.IsWindows()) Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else XdgOpen(url);
    }

    /// <summary>Opens a folder, file or URL with the desktop's default application on Linux.</summary>
    private static void XdgOpen(string target)
    {
        var psi = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        psi.ArgumentList.Add(target);
        Start(psi);
    }

    private static void Start(ProcessStartInfo psi)
    {
        try { Process.Start(psi); } catch { /* nothing useful to report */ }
    }
}
