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
                    Console.WriteLine($"{commandArgs} is a shell builtin");
                else
                    Console.WriteLine($"{input}: command not found");
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
