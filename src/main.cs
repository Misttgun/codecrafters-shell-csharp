using System.Net;
using System.Net.Sockets;

Console.Write("$ ");

// Wait for user input
var command = Console.ReadLine();

while (true)
{
    var words = command?.Split(' ');
    if (words != null && words.Length > 0)
    {
        var firstWord = words[0];
        switch (firstWord)
        {
            case "exit":
                return 0;
            case "echo":
            {
                for (int i = 1; i < words.Length; i++) 
                    Console.Write(words[i] + (i == words.Length - 1 ? "" : " "));
            
                Console.WriteLine();
                break;
            }
            default:
                Console.WriteLine($"{command}: command not found");
                break;
        }
    }
    else
    {
        Console.WriteLine($"{command}: command not found");
    }

    Console.Write("$ ");
    command = Console.ReadLine();
}
