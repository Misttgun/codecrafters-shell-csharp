using ReadLine;
using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type", "pwd", "cd" };

ReadLine.ReadLine.Context.AutoCompletionHandler = new AutoCompleteHandler();

while (true)
{
    // Wait for user input
    var input = ReadLine.ReadLine.Read("$ ");

    if (string.IsNullOrEmpty(input))
        continue;

    var processedInput = ShellHelpers.ParseConsoleText(input.Trim());
    var commands = new List<string>();
    var commandArgs = new List<List<string>>();

    commands.Add(processedInput[0]);
    commandArgs.Add([]);

    // Handle pipeline
    ShellHelpers.ParsePipeline(processedInput, commands, commandArgs);

    string? output = null;
    string? error = null;

    // Handle pipeline (executable only for now)
    if (commands.Count > 1)
    {
        var leftCmd = commands[0];
        var rightCmd = commands[1];

        if (ShellHelpers.TryGetCommandDir(leftCmd, out _) == false)
        {
            Console.Error.Write($"{leftCmd}: command not found\n");
            continue;
        }

        if (ShellHelpers.TryGetCommandDir(rightCmd, out _) == false)
        {
            Console.Error.Write($"{rightCmd}: command not found\n");
            continue;
        }

        using var leftProcess = new Process();
        leftProcess.StartInfo = new ProcessStartInfo
        {
            FileName = leftCmd,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var rightProcess = new Process();
        rightProcess.StartInfo = new ProcessStartInfo
        {
            FileName = rightCmd,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };

        foreach (var arg in commandArgs[0])
            leftProcess.StartInfo.ArgumentList.Add(arg);

        foreach (var arg in commandArgs[1])
            rightProcess.StartInfo.ArgumentList.Add(arg);

        leftProcess.Start();
        rightProcess.Start();

        var copyTask = leftProcess.StandardOutput.BaseStream.CopyToAsync(rightProcess.StandardInput.BaseStream)
            .ContinueWith(_ => rightProcess.StandardInput.Close());

        leftProcess.WaitForExit();
        rightProcess.WaitForExit();
        copyTask.Wait();
    }
    else
    {
        var parsedCmd = new ParsedCommand();

        var commandArgsStr = string.Empty;
        var redirState = RedirState.None;

        if (commandArgs[0].Count > 0)
        {
            foreach (var arg in commandArgs[0])
            {
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
                    parsedCmd.OutputFile = arg;
                    parsedCmd.AppendOutput = redirState == RedirState.AppendOutput;
                }
                else if (redirState is RedirState.RedirectError or RedirState.AppendError)
                {
                    parsedCmd.ErrorFile = arg;
                    parsedCmd.AppendError = redirState == RedirState.AppendError;
                }
                else
                {
                    parsedCmd.Args.Add(arg);
                }
            }

            commandArgsStr = string.Join(' ', parsedCmd.Args);
        }

        parsedCmd.Command = commands[0];

        switch (parsedCmd.Command)
        {
            case "exit":
                return 0;
            case "echo":
            {
                output = $"{commandArgsStr}\n";

                break;
            }
            case "type":
                if (builtinCommands.Contains(commandArgsStr))
                {
                    output = $"{commandArgsStr} is a shell builtin\n";
                }
                else
                {
                    var found = ShellHelpers.TryGetCommandDir(commandArgsStr, out var fullPath);
                    if (found)
                        output = $"{commandArgsStr} is {fullPath}\n";
                    else
                        error = $"{commandArgsStr}: not found\n";
                }

                break;
            case "pwd":
                output = $"{Directory.GetCurrentDirectory()}\n";

                break;
            case "cd":
                var home = Environment.GetEnvironmentVariable("HOME");
                var fallbackHome = Directory.GetCurrentDirectory();

                if (commandArgsStr == "~")
                {
                    Directory.SetCurrentDirectory(home ?? fallbackHome);
                }
                else if (Directory.Exists(commandArgsStr))
                {
                    Directory.SetCurrentDirectory(commandArgsStr);
                }
                else
                {
                    error = $"cd: {commandArgsStr}: No such file or directory\n";
                }

                break;
            default:
                var foundExe = ShellHelpers.TryGetCommandDir(parsedCmd.Command, out _);

                if (foundExe)
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = parsedCmd.Command,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                    };

                    foreach (var arg in parsedCmd.Args)
                        startInfo.ArgumentList.Add(arg);

                    var process = Process.Start(startInfo);

                    output = process?.StandardOutput.ReadToEnd();
                    error = process?.StandardError.ReadToEnd();

                    process?.WaitForExit();
                }
                else
                {
                    error = $"{input}: command not found\n";
                }

                break;
        }

        if (parsedCmd.OutputFile != null)
            ShellHelpers.HandleRedirection(output, parsedCmd.OutputFile, parsedCmd.AppendOutput);
        else
            Console.Write(output);

        if (parsedCmd.ErrorFile != null)
            ShellHelpers.HandleRedirection(error, parsedCmd.ErrorFile, parsedCmd.AppendError);
        else
            Console.Error.Write(error);
    }
}


internal enum RedirState
{
    None,
    RedirectOutput,
    AppendOutput,
    RedirectError,
    AppendError
}

internal class ParsedCommand
{
    public string Command { get; set; } = string.Empty;
    public List<string> Args { get; set; } = [];
    public string? OutputFile;
    public string? ErrorFile;
    public bool AppendOutput;
    public bool AppendError;
}

internal class AutoCompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; } = [' ', '.', '/'];

    public string[] GetSuggestions(string text, int index)
    {
        if (text.StartsWith("ech"))
            return ["echo "];
        if (text.StartsWith("exi"))
            return ["exit "];
        if (text.StartsWith("typ"))
            return ["type "];

        var path = Environment.GetEnvironmentVariable("PATH");
        var pathDirectories = path?.Split(Path.PathSeparator);
        var suggestions = new List<string>();

        if (pathDirectories != null)
        {
            foreach (var directory in pathDirectories)
            {
                if (Directory.Exists(directory) == false)
                    continue;

                foreach (var filePath in Directory.GetFiles(directory))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith(text) && ShellHelpers.HasExecutePermission(filePath))
                    {
                        suggestions.Add(fileName + " ");
                    }
                }
            }
        }

        if (suggestions.Count == 1)
            return suggestions.ToArray();

        if (suggestions.Count > 1)
        {
            var result = suggestions.ToArray();
            Array.Sort(result);

            for (var i = 0; i < result.Length - 1; i++)
            {
                var value = result[i].Trim();

                if (value.StartsWith(text) == false)
                    continue;

                for (var j = i + 1; j < result.Length; j++)
                {
                    var compValue = result[j];
                    if (compValue.StartsWith(value))
                        return [value.Trim()];
                }
            }

            return result;
        }

        Console.Write("\a");
        return null!;
    }
}