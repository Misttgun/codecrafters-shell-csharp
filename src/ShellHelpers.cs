using System.Collections.Generic;
using System.Text;

public static class ShellHelpers
{
    private enum RedirState
    {
        None,
        RedirectOutput,
        AppendOutput,
        RedirectError,
        AppendError
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

    public static ParsedCommand ParseConsoleText(string text)
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

        var parsedCommand = new ParsedCommand
        {
            Command = argList[0]
        };

        var redirState = RedirState.None;
        
        for (var i = 1; i < argList.Count; i++)
        {
            var arg = argList[i];
            switch (arg)
            {
                case ">":
                case "1>":
                    redirState = RedirState.RedirectOutput;
                    continue;
                case "2>":
                    redirState = RedirState.RedirectError;
                    continue;
                case ">>":
                case "1>>":
                    redirState = RedirState.AppendOutput;
                    continue;
                case "2>>":
                    redirState = RedirState.AppendError;
                    continue;
            }

            if (redirState is RedirState.RedirectOutput or RedirState.AppendOutput)
            {
                parsedCommand.OutputFile = arg;
                parsedCommand.AppendOutput = redirState == RedirState.AppendOutput;
            }
            else if (redirState is RedirState.RedirectError or RedirState.AppendError)
            {
                parsedCommand.ErrorFile = arg;
                parsedCommand.AppendError = redirState == RedirState.AppendError;
            }
            else
            {
                parsedCommand.Args.Add(arg);
            }
        }

        return parsedCommand;
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