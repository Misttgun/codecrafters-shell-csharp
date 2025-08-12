using System.Diagnostics;

var builtinCommands = new HashSet<string>() { "exit", "echo", "type" };

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
                    var path = Environment.GetEnvironmentVariable("PATH");
                    var pathDirs = path?.Split(Path.PathSeparator);
                    
                    var found = false;

                    if (pathDirs != null)
                    {
                        foreach (var dir in pathDirs)
                        {
                            var fullPath = Path.Join(dir, commandArgs);
                            if (File.Exists(fullPath) && HasExecutePermission(fullPath))
                            {
                                found = true;
                                Console.WriteLine($"{commandArgs} is {fullPath}");
                                break;
                            }
                        }
                    }
                    
                    if(found == false)
                        Console.WriteLine($"{commandArgs}: not found");
                }
                break;
            default:
                var path1 = Environment.GetEnvironmentVariable("PATH");
                var pathDirs1 = path1?.Split(Path.PathSeparator);

                var found1 = false;

                if (pathDirs1 != null)
                {
                    foreach (var dir in pathDirs1)
                    {
                        var fullPath = Path.Join(dir, command);
                        if (File.Exists(fullPath) && HasExecutePermission(fullPath))
                        {
                            found1 = true;
                            var process = Process.Start(fullPath, commandArgs);
                            process.WaitForExit();
                            
                            break;
                        }
                    }
                }

                if (found1 == false)
                    Console.WriteLine($"{commandArgs}: not found");
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
