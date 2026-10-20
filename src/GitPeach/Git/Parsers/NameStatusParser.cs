namespace GitPeach.Git.Parsers;

public sealed record NameStatusEntry(char Status, string Path, string? OldPath);

public static class NameStatusParser
{
    /// <summary>Parses `--name-status -z` output: "M\0path\0" and "R100\0old\0new\0".</summary>
    public static List<NameStatusEntry> Parse(string output)
    {
        var list = new List<NameStatusEntry>();
        var t = output.Split('\0');
        for (int i = 0; i < t.Length; i++)
        {
            var status = t[i].Trim('\n', '\r');
            if (status.Length == 0) continue;
            char c = status[0];
            if ((c == 'R' || c == 'C') && i + 2 < t.Length)
            {
                list.Add(new NameStatusEntry(c, t[i + 2], t[i + 1]));
                i += 2;
            }
            else if (i + 1 < t.Length)
            {
                list.Add(new NameStatusEntry(c, t[i + 1], null));
                i += 1;
            }
        }
        return list;
    }
}
