using System.Net;
using System.Net.Sockets;

Console.Write("$ ");

// Wait for user input
var command = Console.ReadLine();

while (true)
{
    Console.WriteLine($"{command}: command not found");

    Console.Write("$ ");
    command = Console.ReadLine();
}
