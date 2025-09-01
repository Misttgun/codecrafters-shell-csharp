ReadLine.ReadLine.Context.AutoCompletionHandler = new AutoCompleteHandler();

var shell = new Shell();

while (true)
{
    // Wait for user input
    var input = ReadLine.ReadLine.Read("$ ");

    if (string.IsNullOrEmpty(input))
        continue;

    var parsedCmd = ShellHelpers.ParseConsoleText(input.Trim());

    var (exitCode, output, error) = shell.BuiltinCommands.Contains(parsedCmd.Command)
        ? shell.HandleBuiltInCommand(parsedCmd)
        : shell.HandleExternalCommand(parsedCmd);

    // Handle the exit builtin specific case
    if (exitCode == 0)
        return 0;

    if (parsedCmd.OutputFile != null)
        ShellHelpers.HandleRedirection(output, parsedCmd.OutputFile, parsedCmd.AppendOutput);
    else
        Console.Write(output);

    if (parsedCmd.ErrorFile != null)
        ShellHelpers.HandleRedirection(error, parsedCmd.ErrorFile, parsedCmd.AppendError);
    else
        Console.Error.Write(error);
}