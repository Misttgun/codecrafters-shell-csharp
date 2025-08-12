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
                    
                    Console.WriteLine(path);
                    
                    var found = false;
                    var fullPath = string.Empty;
                    
                    if (pathDirs != null)
                    {
                        Console.WriteLine("PathDirs not null");
                        foreach (var dir in pathDirs)
                        {
                            fullPath = Path.Join(dir, commandArgs);
                            Console.WriteLine(fullPath);
                            if (Directory.Exists(fullPath) == false)
                                continue;

                            found = true;
                        }
                    }
                    
                    if(found)
                        Console.WriteLine($"{commandArgs} is {fullPath}");
                    else
                        Console.WriteLine($"{commandArgs}: not found");
                }
                break;
            default:
                Console.WriteLine($"{input}: command not found");
                break;
        }
    }
    else
    {
        Console.WriteLine($"{input}: command not found");
    }
}
