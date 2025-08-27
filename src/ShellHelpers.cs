using System.Text;


public static class ShellHelpers
{
    public static bool HasExecutePermission(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var unixFileMode = fileInfo.UnixFileMode;
        return (unixFileMode & UnixFileMode.UserExecute) != 0;
    }

    public static bool TryGetCommandDir(string command, out string? fullPath)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        var pathDirectories = path?.Split(Path.PathSeparator);

        fullPath = null;

        if (pathDirectories == null)
            return false;

        foreach (var directory in pathDirectories)
        {
            fullPath = Path.Join(directory, command);
            if (File.Exists(fullPath) == false || HasExecutePermission(fullPath) == false)
                continue;

            return true;
        }

        return false;
    }

    public static List<string> ProcessConsoleText(string text)
    {
        var resultBuilder = new StringBuilder();
        var openSingleQuote = false;
        var openDoubleQuote = false;
        var backSlash = false;
        var argList = new List<string>();

        foreach (var c in text)
        {
            if (c == '\\' && openSingleQuote == false && backSlash == false)
            {
                backSlash = true;
                continue;
            }

            if (c == '"' && openSingleQuote == false && backSlash == false)
            {
                openDoubleQuote = !openDoubleQuote;
                continue;
            }

            if (c == '\'' && openDoubleQuote == false && backSlash == false)
            {
                openSingleQuote = !openSingleQuote;
                continue;
            }

            if (openDoubleQuote || openSingleQuote || backSlash || char.IsWhiteSpace(c) == false)
            {
                HandleBackslashInDoubleQuote(openDoubleQuote, backSlash, c, resultBuilder);

                resultBuilder.Append(c);
                backSlash = false;
                continue;
            }

            if (resultBuilder.Length > 0)
            {
                argList.Add(resultBuilder.ToString());
                resultBuilder.Clear();
            }
        }

        if (resultBuilder.Length > 0)
            argList.Add(resultBuilder.ToString());

        return argList;
    }

    private static void HandleBackslashInDoubleQuote(bool openDoubleQuote, bool backSlash, char c,
        StringBuilder stringBuilder)
    {
        if (openDoubleQuote && backSlash && c != '\\' && c != '"')
            stringBuilder.Append('\\');
    }
}