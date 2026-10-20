using System.Diagnostics;

namespace GitPeach.Services;

public sealed class ShellService : IShellService
{
    public void OpenFolder(string path) =>
        Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });

    public void RevealInExplorer(string path)
    {
        if (File.Exists(path)) Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        else OpenFolder(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
    }

    public void OpenFile(string path) => Start(new ProcessStartInfo(path) { UseShellExecute = true });

    public void OpenTerminal(string directory) =>
        Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = directory, UseShellExecute = true });

    public void OpenInEditor(string path) =>
        Start(new ProcessStartInfo("cmd.exe", $"/c code \"{path}\"") { CreateNoWindow = true, UseShellExecute = false });

    private static void Start(ProcessStartInfo psi)
    {
        try { Process.Start(psi); } catch { /* nothing useful to report */ }
    }
}
