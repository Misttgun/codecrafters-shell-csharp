using System.Text;


public static class ShellHelpers
{
    public static void ParsePipeline(List<string> parsedInput, List<string> commands, List<List<string>> commandArgs)
    {
        if (parsedInput.Count > 1)
        {
            var index = 0;
            var shouldPipe = false;

            for (var i = 1; i < parsedInput.Count; i++)
            {
                if (parsedInput[i] == "|")
                {
                    shouldPipe = true;
                    continue;
                }

                if (shouldPipe)
                {
                    commands.Add(parsedInput[i]);
                    commandArgs.Add([]);
                    index += 1;
                    shouldPipe = false;
                    continue;
                }

                commandArgs[index].Add(parsedInput[i]);
            }
        }
    }
    
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

    public static List<string> ParseConsoleText(string text)
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

    public static async Task HandleRedirectionAsync(StreamReader reader, string filePath, bool append)
    {
        var content = await reader.ReadToEndAsync();
        if (append)
            await File.AppendAllTextAsync(filePath, content);
        else
            await File.WriteAllTextAsync(filePath, content);
    }

    public static void HandleRedirection(string? content, string filePath, bool append)
    {
        if (append)
            File.AppendAllTextAsync(filePath, content);
        else
            File.WriteAllTextAsync(filePath, content);
    }

    private static void HandleBackslashInDoubleQuote(bool openDoubleQuote, bool backSlash, char c,
        StringBuilder stringBuilder)
    {
        if (openDoubleQuote && backSlash && c != '\\' && c != '"')
            stringBuilder.Append('\\');
    }
}