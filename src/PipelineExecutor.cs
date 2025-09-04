using System.Diagnostics;
using System.Text;

namespace cc_shell
{
    /// <summary>
    /// Executes a pipeline of one or more commands, which can be a mix of external processes and shell built-ins.
    /// This implementation uses a simple, synchronous-wait model that is kept deadlock-free
    /// by NOT redirecting the final external command's standard output.
    /// </summary>
    public sealed class PipelineExecutor
    {
        private readonly Shell _shell;
        public PipelineExecutor(Shell shell) => _shell = shell;

        public CommandResult RunPipeline(IReadOnlyList<ParsedCommand> stages)
        {
            if (stages.Count == 0)
                return new CommandResult(0, null, null);

            var processesToWaitFor = new List<Process>();
            StreamReader? inputStreamForNextStage = null;
            string? finalStdout = null;
            string? finalStderr = null;
            int finalExitCode = -1;

            for (int i = 0; i < stages.Count; i++)
            {
                var command = stages[i];
                bool isLastCommand = i == stages.Count - 1;

                if (_shell.IsBuiltin(command.Command))
                {
                    // Built-ins are executed synchronously within our own process.
                    var result = _shell.HandleBuiltInCommand(command, isPipeline: true);

                    if (isLastCommand)
                    {
                        // If the last command is a built-in, we capture its text output.
                        finalStdout = result.Output;
                        finalStderr = result.Error;
                        finalExitCode = result.ExitCode;
                    }
                    else if (string.IsNullOrEmpty(result.Output) == false)
                    {
                        // If a built-in in the middle of a pipeline has output, convert it to a stream to be used as stdin for the next command.
                        var bytes = Encoding.UTF8.GetBytes(result.Output);
                        var memoryStream = new MemoryStream(bytes);
                        inputStreamForNextStage = new StreamReader(memoryStream);
                    }
                    else
                    {
                        // The built-in had no output, so the next stage will receive no input.
                        inputStreamForNextStage = null;
                    }
                }
                else
                {
                    if (ShellHelpers.TryGetCommandDir(command.Command, out _) == false)
                    {
                        if (isLastCommand)
                            return new CommandResult(127, null, $"{command.Command}: command not found\n");
                        inputStreamForNextStage = null;
                        continue;
                    }

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = command.Command,
                        UseShellExecute = false,
                        RedirectStandardInput = inputStreamForNextStage != null,
                        RedirectStandardOutput = isLastCommand == false,
                        RedirectStandardError = true,
                    };

                    foreach (var arg in command.Args)
                        processStartInfo.ArgumentList.Add(arg);

                    var process = Process.Start(processStartInfo)!;

                    if (inputStreamForNextStage != null)
                    {
                        // The input could be from a previous external process OR an in-memory stream from a built-in.
                        var streamToPump = inputStreamForNextStage;
                        Task.Run(async () =>
                        {
                            await streamToPump.BaseStream.CopyToAsync(process.StandardInput.BaseStream);
                            process.StandardInput.Close();
                        });
                    }

                    if (isLastCommand == false)
                    {
                        // This process's output becomes the input for the next stage.
                        inputStreamForNextStage = process.StandardOutput;
                    }
                    else
                    {
                        // This is the last process. We capture its error stream asynchronously.
                        var errorReadTask = process.StandardError.ReadToEndAsync();
                        // We must wait for the process to exit before getting the result.
                        process.WaitForExit();
                        finalStderr = errorReadTask.Result;
                        finalExitCode = process.ExitCode;
                    }

                    processesToWaitFor.Add(process);
                }
            }

            // Wait for any external processes that were started to finish.
            // Built-ins are synchronous, so they are already complete.
            foreach (var process in processesToWaitFor)
            {
                // If it's not the last one, it might already be waited for.
                // A simple WaitForExit is fine here as the I/O is handled by the pumps/last stage.
                if (process.HasExited == false)
                    process.WaitForExit();
            }

            // If the last command was a process, finalExitCode is already set.
            // If it was a built-in, it was set in the built-in handling block.
            return new CommandResult(finalExitCode, finalStdout, finalStderr);
        }
    }
}