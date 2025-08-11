using System.Net;
using System.Net.Sockets;

// Uncomment this line to pass the first stage
Console.Write("$ ");

// Wait for user input
var command = Console.ReadLine();

while (true)
{
    Console.WriteLine($"{command}: command not found");

    command = Console.ReadLine();
}
