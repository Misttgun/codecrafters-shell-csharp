using System.Diagnostics;
using System.Text;

namespace cc_shell
{
    public class Shell
    {
        public static readonly HashSet<string> BuiltinCommands = ["exit", "echo", "type", "pwd", "cd", "history"];
        private static readonly HashSet<string> HistoryArgs = ["-r", "-w", "-a"];

        private int _LastHistoryAppend = -1;
        private readonly string? _HistoryFilePath;

        public static bool IsBuiltin(string command) => BuiltinCommands.Contains(command);

        public Shell()
        {
            _HistoryFilePath = Environment.GetEnvironmentVariable("HISTFILE");
            _ = ReadHistoryFromFileAsync(_HistoryFilePath).GetAwaiter().GetResult();
        }

        public CommandResult HandleBuiltInCommand(ParsedCommand parsedCmd, bool isPipeline = false)
            => HandleBuiltInCommandAsync(parsedCmd, isPipeline).GetAwaiter().GetResult();

        public static CommandResult HandleExternalCommand(ParsedCommand parsedCmd)
            => HandleExternalCommandAsync(parsedCmd).GetAwaiter().GetResult();

        public void WriteHistoryOnExit()
            => WriteHistoryOnExitAsync().GetAwaiter().GetResult();

        private async Task<CommandResult> HandleBuiltInCommandAsync(ParsedCommand parsedCmd, bool isPipeline = false, CancellationToken ct = default)
        {
            var cmd = parsedCmd.Command;
            var args = parsedCmd.Args;
            var commandArgsStr = string.Join(' ', args);

            return cmd switch
            {
                "exit" => HandleExit(commandArgsStr),
                "echo" => HandleEcho(commandArgsStr),
                "type" => HandleType(commandArgsStr),
                "pwd" => HandlePwd(),
                "cd" => HandleCd(commandArgsStr, isPipeline),
                "history" => await HandleHistoryAsync(parsedCmd, isPipeline, ct),
                _ => new CommandResult(0, null, null)
            };
        }

        private static async Task<CommandResult> HandleExternalCommandAsync(ParsedCommand parsedCmd, CancellationToken ct = default)
        {
            string? output = null;
            string? error;

            var foundExe = ShellHelpers.TryGetCommandDir(parsedCmd.Command, out _);

            if (foundExe)
            {
                var startInfo = CreateStartInfo(parsedCmd.Command, parsedCmd.Args);

                using var process = Process.Start(startInfo);
                if (process is null)
                    return new CommandResult(0, null, $"{parsedCmd.Command}: failed to start\n");

                var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
                var stderrTask = process.StandardError.ReadToEndAsync(ct);

                await process.WaitForExitAsync(ct);

                output = await stdoutTask;
                error = await stderrTask;
            }
            else
            {
                error = $"{parsedCmd.Command}: command not found\n";
            }

            return new CommandResult(0, output, error);
        }

        private static CommandResult HandleExit(string commandArgsStr)
        {
            int.TryParse(commandArgsStr, out var exitCode);
            return new CommandResult(exitCode, null, null);
        }

        private static CommandResult HandleEcho(string commandArgsStr)
        {
            var output = $"{commandArgsStr}\n";
            return new CommandResult(0, output, null);
        }

        private static CommandResult HandleType(string commandArgsStr)
        {
            if (IsBuiltin(commandArgsStr))
                return new CommandResult(0, $"{commandArgsStr} is a shell builtin\n", null);

            var found = ShellHelpers.TryGetCommandDir(commandArgsStr, out var fullPath);
            if (found)
                return new CommandResult(0, $"{commandArgsStr} is {fullPath}\n", null);

            return new CommandResult(0, null, $"{commandArgsStr}: not found\n");
        }

        private static CommandResult HandlePwd()
        {
            var output = $"{Directory.GetCurrentDirectory()}\n";
            return new CommandResult(0, output, null);
        }

        private static CommandResult HandleCd(string commandArgsStr, bool isPipeline)
        {
            if (isPipeline) // Don't change directory if we're in a pipeline
                return new CommandResult(0, null, null);

            var home = Environment.GetEnvironmentVariable("HOME");
            var fallbackHome = Directory.GetCurrentDirectory();

            if (commandArgsStr == "~")
            {
                Directory.SetCurrentDirectory(home ?? fallbackHome);
                return new CommandResult(0, null, null);
            }

            if (Directory.Exists(commandArgsStr) == false)
                return new CommandResult(0, null, $"cd: {commandArgsStr}: No such file or directory\n");

            Directory.SetCurrentDirectory(commandArgsStr);
            return new CommandResult(0, null, null);
        }

        private async Task<CommandResult> HandleHistoryAsync(ParsedCommand parsedCmd, bool isPipeline, CancellationToken ct)
        {
            if (isPipeline) // Don't process history if we're in a pipeline (it should empty)
                return new CommandResult(0, null, null);

            var args = parsedCmd.Args;
            var commandArgsStr = string.Join(' ', args);

            var startIndex = 0;

            if (args.Count > 0) // If we pass arguments
            {
                var firstArg = args[0];
                if (HistoryArgs.Contains(firstArg))
                {
                    var filePath = args.Count == 2 ? args[1] : null;

                    if (filePath == null)
                        return new CommandResult(0, null, $"history: {commandArgsStr} is not a valid argument\n");

                    if (firstArg == "-r") // Read history from file and add it to current history
                    {
                        var readErr = await ReadHistoryFromFileAsync(filePath, ct);
                        return new CommandResult(0, null, readErr);
                    }

                    if (firstArg == "-w") // Write history to file
                    {
                        await WriteHistoryToFileAsync(filePath, ct);
                        return new CommandResult(0, null, null);
                    }

                    if (firstArg == "-a") // Append history to file
                    {
                        await AppendHistoryToFileAsync(filePath, ct);
                        return new CommandResult(0, null, null);
                    }
                }
                else if (int.TryParse(string.Join(' ', args), out var historyCount))
                {
                    startIndex = Math.Max(0, ReadLine.ReadLine.Context.History.Count - historyCount);
                }
                else
                {
                    return new CommandResult(0, null, $"history: {commandArgsStr} is not a valid argument\n");
                }
            }

            var builder = new StringBuilder();
            var history = ReadLine.ReadLine.Context.History;
            for (var i = startIndex; i < history.Count; i++)
            {
                var line = history[i];
                builder.AppendLine($"    {i + 1}  {line}");
            }

            return new CommandResult(0, builder.ToString(), null);
        }

        private async Task AppendHistoryToFileAsync(string filePath, CancellationToken ct = default)
        {
            var history = ReadLine.ReadLine.Context.History;
            var historyToAppend = _LastHistoryAppend == -1
                ? history
                : history[_LastHistoryAppend..];

            _LastHistoryAppend = history.Count;

            await File.AppendAllLinesAsync(filePath, historyToAppend, ct);
        }

        private static ProcessStartInfo CreateStartInfo(string fileName, IReadOnlyList<string> args)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            return startInfo;
        }

        private static async Task<string?> ReadHistoryFromFileAsync(string? filePath, CancellationToken ct = default)
        {
            if (filePath == null)
                return null;

            if (File.Exists(filePath) == false)
                return $"history: {filePath} is not a valid path\n";

            var lines = await File.ReadAllLinesAsync(filePath, ct);
            ReadLine.ReadLine.Context.History.AddRange(lines);

            return null;
        }

        private static async Task WriteHistoryToFileAsync(string? filePath, CancellationToken ct = default)
        {
            if (filePath == null)
                return;

            await File.WriteAllLinesAsync(filePath, ReadLine.ReadLine.Context.History, ct);
        }

        private async Task WriteHistoryOnExitAsync(CancellationToken ct = default)
        {
            await WriteHistoryToFileAsync(_HistoryFilePath, ct);
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