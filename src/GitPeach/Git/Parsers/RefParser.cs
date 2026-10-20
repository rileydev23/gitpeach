using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class RefParser
{
    public const string Format = "--format=%(refname)%1f%(objectname)%1f%(HEAD)%1f%(upstream:short)%1f%(upstream:track)%1f%(*objectname)";

    public static RefsSnapshot Parse(string output, IEnumerable<string> remoteNames)
    {
        var snap = new RefsSnapshot();
        snap.RemoteNames.AddRange(remoteNames);
        var sig = new System.Text.StringBuilder();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.TrimEnd('\r').Split('\x1f');
            if (f.Length < 6) continue;
            var full = f[0];
            sig.Append(full).Append('=').Append(f[1]).Append(';');

            if (full.StartsWith("refs/heads/"))
            {
                snap.Locals.Add(new BranchInfo
                {
                    Name = full[11..], FullName = full, Hash = f[1],
                    IsCurrent = f[2] == "*",
                    Upstream = f[3].Length > 0 ? f[3] : null,
                    Track = f[4].Length > 0 ? f[4] : null,
                });
            }
            else if (full.StartsWith("refs/remotes/"))
            {
                var name = full[13..];
                if (name.EndsWith("/HEAD")) continue;
                var remote = snap.RemoteNames.FirstOrDefault(r => name.StartsWith(r + "/"))
                             ?? name[..name.IndexOf('/')];
                snap.Remotes.Add(new BranchInfo
                {
                    Name = name, FullName = full, Hash = f[1], IsRemote = true, Remote = remote,
                });
            }
            else if (full.StartsWith("refs/tags/"))
            {
                // Annotated tags: the peeled commit is in %(*objectname).
                snap.Tags.Add(new TagInfo(full[10..], f[5].Length > 0 ? f[5] : f[1]));
            }
        }
        snap.Signature = sig.ToString();
        return snap;
    }
}
