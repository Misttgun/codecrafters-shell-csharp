var path = Environment.GetEnvironmentVariable("PATH");
var pathDirs = path?.Split(Path.PathSeparator);

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
                if (commandArgs is "echo" or "exit" or "type")
                {
                    Console.WriteLine($"{commandArgs} is a shell builtin");
                }
                else
                {
                    var found = false;
                    var foundDir = string.Empty;
                    if (pathDirs != null)
                    {
                        foreach (var dir in pathDirs)
                        {
                            var files = Directory.EnumerateFiles(dir, ".exe", SearchOption.AllDirectories);
                            if (files.Contains(commandArgs))
                            {
                                found = true;
                                foundDir = dir;
                                break;
                            }
                        }
                    }
                    
                    if(found)
                        Console.WriteLine($"{commandArgs} is {foundDir}");
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
