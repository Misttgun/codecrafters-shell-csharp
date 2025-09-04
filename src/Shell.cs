using System.Diagnostics;
using System.Text;

namespace cc_shell
{
    public class Shell
    {
        public static readonly HashSet<string> BuiltinCommands = ["exit", "echo", "type", "pwd", "cd", "history"];
        private readonly HashSet<string> _HistoryArgs = ["-r", "-w", "-a"];

        private int _LastHistoryAppend = -1;
        private readonly string? _HistoryFilePath;
        
        public static bool IsBuiltin(string command) => BuiltinCommands.Contains(command);

        public Shell()
        {
            _HistoryFilePath = Environment.GetEnvironmentVariable("HISTFILE");
            ReadHistoryFromFile(_HistoryFilePath);
        }

        public CommandResult HandleBuiltInCommand(ParsedCommand parsedCmd, bool isPipeline = false)
        {
            var commandArgsStr = string.Join(' ', parsedCmd.Args);
            string? output = null;
            string? error = null;
            var exitCode = 0;

            switch (parsedCmd.Command)
            {
                case "exit":
                    int.TryParse(commandArgsStr,out exitCode);

                    break;
                case "echo":
                {
                    output = $"{commandArgsStr}\n";

                    break;
                }
                case "type":
                    if (IsBuiltin(commandArgsStr))
                    {
                        output = $"{commandArgsStr} is a shell builtin\n";
                    }
                    else
                    {
                        var found = ShellHelpers.TryGetCommandDir(commandArgsStr, out var fullPath);
                        if (found)
                            output = $"{commandArgsStr} is {fullPath}\n";
                        else
                            error = $"{commandArgsStr}: not found\n";
                    }

                    break;
                case "pwd":
                    output = $"{Directory.GetCurrentDirectory()}\n";

                    break;
                case "cd":
                    if (isPipeline) // Don't change directory if we're in a pipeline
                        break;
                    
                    var home = Environment.GetEnvironmentVariable("HOME");
                    var fallbackHome = Directory.GetCurrentDirectory();

                    if (commandArgsStr == "~")
                    {
                        Directory.SetCurrentDirectory(home ?? fallbackHome);
                    }
                    else if (Directory.Exists(commandArgsStr))
                    {
                        Directory.SetCurrentDirectory(commandArgsStr);
                    }
                    else
                    {
                        error = $"cd: {commandArgsStr}: No such file or directory\n";
                    }

                    break;
                case "history":
                    if (isPipeline) // Don't process history if we're in a pipeline (it should empty)
                        break;
                    
                    var startIndex = 0;
                    if (parsedCmd.Args.Count > 0) // If we pass arguments
                    {
                        if (_HistoryArgs.Contains(parsedCmd.Args[0]))
                        {
                            var historyArg = parsedCmd.Args[0];
                            var filePath = parsedCmd.Args.Count == 2 ? parsedCmd.Args[1] : null;

                            if (filePath == null)
                            {
                                error = $"history: {commandArgsStr} is not a valid argument\n";
                                break;
                            }

                            if (historyArg == "-r") // Read history from file and add it to current history
                            {
                                error = ReadHistoryFromFile(filePath);
                                break;
                            }

                            if (historyArg == "-w") // Write history to file
                            {
                                WriteHistoryToFile(filePath);
                                break;
                            }

                            if (historyArg == "-a") // Append history to file
                            {
                                var historyToAppend = _LastHistoryAppend == -1
                                    ? ReadLine.ReadLine.Context.History
                                    : ReadLine.ReadLine.Context.History[_LastHistoryAppend..];

                                _LastHistoryAppend = ReadLine.ReadLine.Context.History.Count;

                                File.AppendAllLines(filePath, historyToAppend);
                                break;
                            }
                        }
                        else if (int.TryParse(commandArgsStr, out var historyCount))
                        {
                            startIndex = Math.Max(0, ReadLine.ReadLine.Context.History.Count - historyCount);
                        }
                        else
                        {
                            error = $"history: {commandArgsStr} is not a valid argument\n";
                            break;
                        }
                    }

                    var builder = new StringBuilder();

                    for (var i = startIndex; i < ReadLine.ReadLine.Context.History.Count; i++)
                    {
                        var line = ReadLine.ReadLine.Context.History[i];
                        builder.AppendLine($"    {i + 1}  {line}");
                    }

                    output = builder.ToString();
                    break;
            }

            return new CommandResult(exitCode, output, error);
        }

        public CommandResult HandleExternalCommand(ParsedCommand parsedCmd)
        {
            string? output = null;
            string? error;

            var foundExe = ShellHelpers.TryGetCommandDir(parsedCmd.Command, out _);

            if (foundExe)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = parsedCmd.Command,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                foreach (var arg in parsedCmd.Args)
                    startInfo.ArgumentList.Add(arg);

                var process = Process.Start(startInfo);

                output = process?.StandardOutput.ReadToEnd();
                error = process?.StandardError.ReadToEnd();

                process?.WaitForExit();
            }
            else
            {
                error = $"{parsedCmd.Command}: command not found\n";
            }

            return new CommandResult(0, output, error);
        }

        private static string? ReadHistoryFromFile(string? filePath)
        {
            if (filePath == null)
                return null;

            if (File.Exists(filePath) == false)
                return $"history: {filePath} is not a valid path\n";

            ReadLine.ReadLine.Context.History.AddRange(File.ReadAllLines(filePath));

            return null;
        }

        private static void WriteHistoryToFile(string? filePath)
        {
            if (filePath == null)
                return;

            File.WriteAllLines(filePath, ReadLine.ReadLine.Context.History);
        }

        public void WriteHistoryOnExit()
        {
            WriteHistoryToFile(_HistoryFilePath);
        }
    }

    public class ParsedCommand
    {
        public string Command { get; init; } = string.Empty;
        public List<string> Args { get; } = [];
        public string? OutputFile;
        public string? ErrorFile;
        public bool AppendOutput;
        public bool AppendError;
    }

    public record CommandResult(int ExitCode, string? Output, string? Error);
}