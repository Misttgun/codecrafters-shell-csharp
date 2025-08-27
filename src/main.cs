using System.Diagnostics;
using System.Text;
using ReadLine;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type", "pwd", "cd" };

List<string> argsList = new List<string>();

ReadLine.ReadLine.Context.AutoCompletionHandler = new AutoCompleteHandler();

while (true)
{
    Console.Write("$ ");

    // Wait for user input
    var input = ReadLine.ReadLine.Read();

    if (string.IsNullOrEmpty(input))
        continue;

    string? outputFile = null;
    string? errorFile = null;
    argsList.Clear();

    var processedInput = ShellHelpers.ProcessConsoleText(input.Trim());
    var command = processedInput[0];
    var commandArgs = string.Empty;

    var redirState = RedirState.None;

    if (processedInput.Count > 1)
    {
        for (var i = 1; i < processedInput.Count; i++)
        {
            switch (processedInput[i])
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
                outputFile = processedInput[i];
                break;
            }

            if (redirState is RedirState.RedirectError or RedirState.AppendError)
            {
                errorFile = processedInput[i];
                break;
            }

            argsList.Add(processedInput[i]);
        }

        commandArgs = string.Join(' ', argsList);
    }

    string? output = null;
    string? error = null;

    switch (command)
    {
        case "exit":
            return 0;
        case "echo":
        {
            output = $"{commandArgs}\n";

            break;
        }
        case "type":
            if (builtinCommands.Contains(commandArgs))
            {
                output = $"{commandArgs} is a shell builtin\n";
            }
            else
            {
                var found = ShellHelpers.TryGetCommandDir(commandArgs, out var fullPath);
                if (found)
                    output = $"{commandArgs} is {fullPath}\n";
                else
                    error = $"{commandArgs}: not found\n";
            }

            break;
        case "pwd":
            output = $"{Directory.GetCurrentDirectory()}\n";

            break;
        case "cd":
            var home = Environment.GetEnvironmentVariable("HOME");
            var fallbackHome = Directory.GetCurrentDirectory();

            if (commandArgs == "~")
            {
                Directory.SetCurrentDirectory(home ?? fallbackHome);
            }
            else if (Directory.Exists(commandArgs))
            {
                Directory.SetCurrentDirectory(commandArgs);
            }
            else
            {
                error = $"cd: {commandArgs}: No such file or directory\n";
            }

            break;
        default:
            var foundExe = ShellHelpers.TryGetCommandDir(command, out _);

            if (foundExe)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                foreach (var arg in argsList)
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

    if (outputFile != null)
    {
        if (redirState == RedirState.RedirectOutput)
            File.WriteAllText(outputFile, output);
        else if (redirState == RedirState.AppendOutput)
            File.AppendAllText(outputFile, output);
    }
    else
    {
        Console.Write(output);
    }

    if (errorFile != null)
    {
        if (redirState == RedirState.RedirectError)
            File.WriteAllText(errorFile, error);
        else if (redirState == RedirState.AppendError)
            File.AppendAllText(errorFile, error);
    }
    else
    {
        Console.Write(error);
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
                        //Console.Write(" " + fileName + " ");
                    }
                }
            }
        }

        if (suggestions.Count > 0)
            return suggestions.ToArray();
        
        Console.Write("\a");
        return null!;
    }
}