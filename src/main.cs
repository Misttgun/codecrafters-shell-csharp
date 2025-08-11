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
        string firstWord = words[0];
        if (firstWord == "exit")
            return 0;
    }
    
    Console.WriteLine($"{command}: command not found");

    Console.Write("$ ");
    command = Console.ReadLine();
}
