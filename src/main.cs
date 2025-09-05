using cc_shell;

ReadLine.ReadLine.Context.AutoCompletionHandler = new AutoCompleteHandler();
ReadLine.ReadLine.Context.HistoryEnabled = true;

var shell = new Shell();

while (true)
{
    // Wait for user input
    var input = ReadLine.ReadLine.Read("$ ");

    if (string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
        continue;

    // Detect pipelines and split into segments (respecting quotes and escapes)
    var segments = ShellHelpers.SplitPipelineSegments(input.Trim());

    string? output;
    string? error;

    if (segments.Count > 1)
    {
        // Parse each segment into a ParsedCommand
        var parsedCommands = new List<ParsedCommand>();
        foreach (var seg in segments)
        {
            var parsed = ShellHelpers.ParseConsoleText(seg.Trim());
            parsedCommands.Add(parsed);
        }

        var pipelineExecutor = new PipelineExecutor(shell);
        (_, output, error) = pipelineExecutor.RunPipeline(parsedCommands);

        if (string.IsNullOrEmpty(output) == false)
            Console.Write(output);
        else if (string.IsNullOrEmpty(error) == false)
            Console.Error.Write(error);

        continue;
    }

    var parsedCmd = ShellHelpers.ParseConsoleText(input.Trim());

    (var exitCode, output, error) = Shell.IsBuiltin(parsedCmd.Command)
        ? shell.HandleBuiltInCommand(parsedCmd)
        : shell.HandleExternalCommand(parsedCmd);

    // Handle the exit builtin specific case
    if (parsedCmd.Command == "exit")
    {
        shell.WriteHistoryOnExit();
        return exitCode;
    }

    if (parsedCmd.OutputFile != null)
        ShellHelpers.HandleRedirection(output, parsedCmd.OutputFile, parsedCmd.AppendOutput);
    else
        Console.Write(output);

    if (parsedCmd.ErrorFile != null)
        ShellHelpers.HandleRedirection(error, parsedCmd.ErrorFile, parsedCmd.AppendError);
    else
        Console.Error.Write(error);
}