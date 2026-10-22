using System.Diagnostics;
using System.Text;
using GitPeach.Services;

namespace GitPeach.Git;

public sealed record GitResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Ok => ExitCode == 0;
    public string Message => string.IsNullOrWhiteSpace(Stderr) ? Stdout.Trim() : Stderr.Trim();

    public static GitResult Success(string stdout = "") => new(0, stdout, "");
    public static GitResult Fail(string message) => new(-1, "", message);
}

/// <summary>Runs the git executable. The only place that spawns git processes.</summary>
public interface IGitRunner
{
    string GitExe { get; set; }

    Task<GitResult> RunAsync(
        string cwd,
        IReadOnlyList<string> args,
        string? stdin = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default);
}

public sealed class GitRunner : IGitRunner
{
    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly string[] BaseArgs =
    {
        "--no-pager",
        "-c", "core.quotepath=false",
        "-c", "color.ui=false",
        "-c", "i18n.logOutputEncoding=UTF-8",
    };

    public string GitExe { get; set; } = "git";

    public async Task<GitResult> RunAsync(
        string cwd,
        IReadOnlyList<string> args,
        string? stdin = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(GitExe)
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8,
            StandardInputEncoding = Utf8,
        };
        foreach (var a in BaseArgs) psi.ArgumentList.Add(a);
        foreach (var a in args) psi.ArgumentList.Add(a);
        // Never block on a terminal prompt; Git Credential Manager still shows its own GUI.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // Read-only commands like status must not rewrite the index (avoids watcher feedback loops).
        psi.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        // Paths are passed verbatim, never as globs.
        psi.Environment["GIT_LITERAL_PATHSPECS"] = "1";
        psi.Environment["GIT_EDITOR"] = "true";
        psi.Environment["GIT_MERGE_AUTOEDIT"] = "no";

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            return GitResult.Fail(Loc.F("git.runError", ex.Message));
        }

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = onStderr is null
            ? proc.StandardError.ReadToEndAsync(ct)
            : PumpAsync(proc.StandardError, onStderr, ct);

        try
        {
            if (stdin is not null) await proc.StandardInput.WriteAsync(stdin.AsMemory(), ct);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new GitResult(proc.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
            throw;
        }
        catch (IOException ex)
        {
            // Process exited before reading stdin, etc.
            await proc.WaitForExitAsync(CancellationToken.None);
            return new GitResult(proc.ExitCode == 0 ? -1 : proc.ExitCode, "", ex.Message);
        }
    }

    private static async Task<string> PumpAsync(StreamReader reader, Action<string> onChunk, CancellationToken ct)
    {
        var all = new StringBuilder();
        var buf = new char[4096];
        int n;
        while ((n = await reader.ReadAsync(buf.AsMemory(), ct)) > 0)
        {
            var chunk = new string(buf, 0, n);
            all.Append(chunk);
            onChunk(chunk);
        }
        return all.ToString();
    }
}
