using System.Diagnostics;

namespace cc_shell
{
    public sealed class PipelineExecutor
    {
        private readonly Shell _shell;
        public PipelineExecutor(Shell shell) => _shell = shell;
        
        public CommandResult RunPipeline(IReadOnlyList<ParsedCommand> stages)
        {
            if (stages.Count == 0)
            {
                return new CommandResult(0, null, null);
            }
            
            var processes = new List<Process>();
            StreamReader? previousProcessOutput = null;

            for (int i = 0; i < stages.Count; i++)
            {
                var command = stages[i];
                bool isLastCommand = i == stages.Count - 1;

                // Validate that the command exists before attempting to start it.
                if (ShellHelpers.TryGetCommandDir(command.Command, out _) == false)
                {
                    // To mimic bash behavior, we only report an error if the *last* command is not found.
                    // If an intermediate command is missing, the pipeline will just fail silently.
                    if (isLastCommand)
                        return new CommandResult(127, null, $"{command.Command}: command not found\n");
                    
                    // Stop piping data from this point forward.
                    previousProcessOutput = null;
                    continue;
                }

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = command.Command,
                    UseShellExecute = false,
                    RedirectStandardInput = i > 0, // Redirect stdin if this is NOT the first command in the pipeline.
                    RedirectStandardOutput = isLastCommand == false, // Redirect stdout ONLY if this is NOT the last command in the pipeline.
                    RedirectStandardError = true
                };
                
                foreach (var arg in command.Args)
                    processStartInfo.ArgumentList.Add(arg);

                var process = Process.Start(processStartInfo)!;

                // If there was a previous process, connect its output to this process's input.
                if (previousProcessOutput != null)
                {
                    // This task runs in the background to pump data from the previous process's stdout to the current process's stdin.
                    Task.Run(async () =>
                    {
                        await previousProcessOutput.BaseStream.CopyToAsync(process.StandardInput.BaseStream);
                        
                        // Once the copy is complete (because the previous stream closed), we must close the current process's input stream to signal EOF.
                        process.StandardInput.Close();
                    });
                }

                // If this isn't the last command, its output will be the input for the next one.
                if (isLastCommand == false)
                    previousProcessOutput = process.StandardOutput;

                processes.Add(process);
            }
            
            var lastProcess = processes.Last();
            var errorReadTask = lastProcess.StandardError.ReadToEndAsync();
            
            // Wait for all processes in the pipeline to complete.
            // It's important to wait for all of them to prevent "zombie" processes.
            foreach (var process in processes)
                process.WaitForExit();

            // After all processes have exited, we can safely get the result of the error-reading task.
            string? finalError = errorReadTask.Result;
            int finalExitCode = lastProcess.ExitCode;


            // In this simplified model, we do not capture stdout or stderr.
            // The last process writes directly to the console, so we return null for output.
            return new CommandResult(finalExitCode, null, finalError);
        }
    }
}