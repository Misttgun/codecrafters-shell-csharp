using System.Diagnostics;
using System.Text;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type", "pwd", "cd" };

List<string> argsList = new List<string>();

while (true)
{
    Console.Write("$ ");

    // Wait for user input
    var input = Console.ReadLine();

    if (input == null)
        continue;

    string? outputFile = null;
    string? errorFile = null;
    argsList.Clear();

    var processedInput = ProcessConsoleText(input.Trim());
    var command = processedInput[0];
    var commandArgs = string.Empty;

    if (processedInput.Count > 1)
    {
        var redirect = false;
        var errorRedirect = false;
        for (var i = 1; i < processedInput.Count; i++)
        {
            switch (processedInput[i])
            {
                case ">":
                case "1>":
                    redirect = true;
                    continue;
                case "2>":
                    errorRedirect = true;
                    continue;
            }

            if (redirect)
            {
                outputFile = processedInput[i];
                break;
            }

            if (errorRedirect)
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
            //if (string.IsNullOrEmpty(outputFile) == false)
            //{
            //    File.WriteAllText(outputFile, commandArgs + Environment.NewLine);
            //}
            //else if (string.IsNullOrEmpty(errorFile) == false)
            //{
            //    Console.WriteLine(commandArgs);
            //    if (File.Exists(errorFile) == false)
            //    {
            //        using (File.Create(errorFile)) ;
            //    }
            //}
            //else
            //{
            //    Console.WriteLine(commandArgs);
            //}

            output = $"{commandArgs}\n";

            break;
        }
        case "type":
            if (builtinCommands.Contains(commandArgs))
            {
                output = $"{commandArgs} is a shell builtin\n";
                //Console.WriteLine($"{commandArgs} is a shell builtin");
            }
            else
            {
                var found = TryGetCommandDir(commandArgs, out var fullPath);
                if (found)
                    output = $"{commandArgs} is {fullPath}\n";
                else
                    error = $"{commandArgs}: not found\n";
                //Console.WriteLine(found ? $"{commandArgs} is {fullPath}" : $"{commandArgs}: not found");
            }

            break;
        case "pwd":
            output = $"{Directory.GetCurrentDirectory()}\n";
            //Console.WriteLine(Directory.GetCurrentDirectory());

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
                //Console.WriteLine($"cd: {commandArgs}: No such file or directory");
            }

            break;
        default:
            var foundExe = TryGetCommandDir(command, out _);

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
        File.WriteAllText(outputFile, output);
    else
        Console.Write(output);

    if (errorFile != null)
        File.WriteAllText(errorFile, error);
    else
        Console.Write(error);
}

bool HasExecutePermission(string filePath)
{
    var fileInfo = new FileInfo(filePath);
    var unixFileMode = fileInfo.UnixFileMode;
    return (unixFileMode & UnixFileMode.UserExecute) != 0;
}

bool TryGetCommandDir(string command, out string? fullPath)
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

List<string> ProcessConsoleText(string text)
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

static void HandleBackslashInDoubleQuote(bool openDoubleQuote, bool backSlash, char c, StringBuilder stringBuilder)
{
    if (openDoubleQuote && backSlash && c != '\\' && c != '"')
        stringBuilder.Append('\\');
}