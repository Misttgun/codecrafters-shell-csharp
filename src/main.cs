using System.Diagnostics;
using System.Text;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type", "pwd", "cd" };

List<string> redirectArgs = new List<string>();
List<string> argsList = new List<string>();

while (true)
{
    Console.Write("$ ");

    // Wait for user input
    var input = Console.ReadLine();

    if (input == null)
        continue;

    redirectArgs.Clear();
    argsList.Clear();
    
    var processedInput = ProcessConsoleText(input.Trim());
    var command = processedInput[0];
    var commandArgs = string.Empty;
    
    if (processedInput.Count > 1)
    {
        var beforeRedirect = true;
        for (var i = 1; i < processedInput.Count; i++)
        {
            if (processedInput[i] == ">" || processedInput[i] == "1>")
            {
                beforeRedirect = false;
                continue;
            }
            
            if (beforeRedirect)
                argsList.Add(processedInput[i]);
            else
                redirectArgs.Add(processedInput[i]);
        }
        
        //argsList = processedInput[1..];
        commandArgs = string.Join(' ', argsList);
    }

    switch (command)
    {
        case "exit":
            return 0;
        case "echo":
        {
            if (redirectArgs.Count > 0)
            {
                File.WriteAllText(redirectArgs[0], commandArgs + Environment.NewLine);
            }
            else
            {
                Console.WriteLine(commandArgs);
            }

            break;
        }
        case "type":
            if (builtinCommands.Contains(commandArgs))
            {
                Console.WriteLine($"{commandArgs} is a shell builtin");
            }
            else
            {
                var found = TryGetCommandDir(commandArgs, out var fullPath);
                Console.WriteLine(found ? $"{commandArgs} is {fullPath}" : $"{commandArgs}: not found");
            }

            break;
        case "pwd":
            Console.WriteLine(Directory.GetCurrentDirectory());

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
                Console.WriteLine($"cd: {commandArgs}: No such file or directory");
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
                var output = process?.StandardOutput.ReadToEnd();
                var error = process?.StandardError.ReadToEnd();
                process?.WaitForExit();

                if (redirectArgs.Count > 0)
                {
                    //Console.WriteLine($"Redirect args {redirectArgs[0]}");
                    File.WriteAllText(redirectArgs[0], output);
                }
                else
                {
                    Console.Write(output);
                }


                if (string.IsNullOrEmpty(error) == false)
                    Console.Write(error);
            }
            else
            {
                Console.WriteLine($"{input}: command not found");
            }

            break;
    }
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