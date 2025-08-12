using System.Diagnostics;

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
        var commandArgs = input[command.Length..].Trim();

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
                if (Directory.Exists(commandArgs))
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