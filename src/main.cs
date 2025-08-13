using System.Diagnostics;
using System.Text;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type", "pwd", "cd" };

while (true)
{
    Console.Write("$ ");

    // Wait for user input
    var input = Console.ReadLine();

    if (input == null)
        continue;

    var words = input.Split(' ');
    if (words.Length > 0)
    {
        var command = words[0];
        var cArgs = input[command.Length..].Trim();
        var commandArgs = ProcessArguments(cArgs).ToString();

        switch (command)
        {
            case "exit":
                return 0;
            case "echo":
            {
                Console.WriteLine(commandArgs);

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
                    Console.WriteLine($"Found {command}");
                    var process = Process.Start(command, commandArgs);
                    process.WaitForExit();
                }
                else
                {
                    Console.WriteLine($"{input}: command not found");
                }

                break;
        }
    }

    else
    {
        Console.WriteLine($"{input}: command not found");
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

string ProcessArguments(string arguments)
{
    var resultBuilder = new StringBuilder();
    var inSingleQuote = false;
    var argList = new List<string>();
    
    foreach (var c in arguments)
    {
        if (c == '\'')
        {
            inSingleQuote = !inSingleQuote;
            continue;
        }

        if (char.IsWhiteSpace(c) == false || inSingleQuote)
        {
            resultBuilder.Append(c);
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

    return string.Join(' ', argList);
}