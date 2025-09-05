using System.Runtime.CompilerServices;
using System.Text;

namespace cc_shell
{
    /// <summary>
    /// Provides utility methods for shell operations including command resolution, text parsing, and I/O redirection.
    /// </summary>
    public static class ShellHelpers
    {
        private enum RedirState
        {
            None,
            RedirectOutput,
            AppendOutput,
            RedirectError,
            AppendError
        }

        // Cache the user's home directory for tilde expansion
        private static readonly string? HomeDirectory = Environment.GetEnvironmentVariable("HOME");

        /// <summary>
        /// Determines if a file has execute permissions for the current user.
        /// </summary>
        public static bool HasExecutePermission(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var unixFileMode = fileInfo.UnixFileMode;
                
                // Check user, group, and world execute permissions (Linux-specific)
                return (unixFileMode & UnixFileMode.UserExecute) != 0 ||
                       (unixFileMode & UnixFileMode.GroupExecute) != 0 ||
                       (unixFileMode & UnixFileMode.OtherExecute) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves a command name to its full path by searching in PATH directories.
        /// Supports absolute paths, relative paths, and handles tilde expansion.
        /// </summary>
        public static bool TryGetCommandDir(string command, out string? fullPath)
        {
            fullPath = null;
            
            if (string.IsNullOrWhiteSpace(command))
                return false;
                
            // Handle tilde expansion for the home directory (Linux shell feature)
            command = ExpandPath(command);

            // Absolute or relative path with directory components
            if (Path.IsPathRooted(command) || command.Contains('/'))
            {
                if (File.Exists(command) == false || HasExecutePermission(command) == false) 
                    return false;
                
                fullPath = command;
                return true;
            }

            // Look in PATH directories
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return false;
                
            var pathDirectories = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var directory in pathDirectories)
            {
                try 
                {
                    if (string.IsNullOrEmpty(directory))
                        continue;
                        
                    var candidatePath = Path.Join(directory, command);
                    if (File.Exists(candidatePath) && HasExecutePermission(candidatePath))
                    {
                        fullPath = candidatePath;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Skip inaccessible directories
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the character was fully handled (toggle/escape) and should NOT be appended by the caller.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleQuoteAndEscape(char c, ref bool openSingleQuote, ref bool openDoubleQuote, ref bool backSlash,
            StringBuilder appendTarget)
        {
            // Cache flags to avoid repeated loads
            var inSingle = openSingleQuote;
            var inDouble = openDoubleQuote;
            var escaped = backSlash;

            switch (c)
            {
                case '\\':
                    if (!inSingle && !escaped)
                    {
                        backSlash = true;
                        return true;
                    }
                    break;

                case '"':
                    if (!inSingle && !escaped)
                    {
                        openDoubleQuote = !inDouble;
                        return true;
                    }
                    break;

                case '\'':
                    if (!inDouble && !escaped)
                    {
                        openSingleQuote = !inSingle;
                        return true;
                    }
                    break;
            }

            // Handle backslash within double quotes
            if (inDouble && escaped && c != '\\' && c != '"')
                appendTarget.Append('\\');

            return false;
        }

        /// <summary>
        /// Splits a command string into pipeline segments, respecting quotes and escaping.
        /// </summary>
        public static List<string> SplitPipelineSegments(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];
                
            var segments = new List<string>();
            var current = new StringBuilder();

            var openSingleQuote = false;
            var openDoubleQuote = false;
            var backSlash = false;

            foreach (var c in text)
            {
                if (HandleQuoteAndEscape(c, ref openSingleQuote, ref openDoubleQuote, ref backSlash, current))
                    continue;

                if (c == '|' && !openSingleQuote && !openDoubleQuote && backSlash == false)
                {
                    segments.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
                backSlash = false;
            }

            // Capture remaining segment
            if (current.Length > 0)
                segments.Add(current.ToString().Trim());

            segments.RemoveAll(string.IsNullOrWhiteSpace);
            
            return segments;
        }

        /// <summary>
        /// Parses a command string into a structured ParsedCommand object,
        /// handling quotes, escaping, and redirection operators.
        /// </summary>
        public static ParsedCommand ParseConsoleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new ParsedCommand { Command = string.Empty };
                
            var resultBuilder = new StringBuilder();
            var openSingleQuote = false;
            var openDoubleQuote = false;
            var backSlash = false;
            var argList = new List<string>();

            // Tokenize the input
            foreach (var c in text)
            {
                if (HandleQuoteAndEscape(c, ref openSingleQuote, ref openDoubleQuote, ref backSlash, resultBuilder))
                    continue;

                if (openDoubleQuote || openSingleQuote || backSlash || char.IsWhiteSpace(c) == false)
                {
                    resultBuilder.Append(c);
                    backSlash = false;
                    continue;
                }

                if (resultBuilder.Length <= 0)
                    continue;

                argList.Add(resultBuilder.ToString());
                resultBuilder.Clear();
            }

            // Handle any remaining token
            if (resultBuilder.Length > 0)
                argList.Add(resultBuilder.ToString());
            
            // Handle empty input
            if (argList.Count == 0)
                return new ParsedCommand { Command = string.Empty };

            // Process the tokens into a command
            var parsedCommand = new ParsedCommand
            {
                Command = argList[0]
            };

            // Process redirection and arguments
            ParseRedirectionAndArguments(argList, parsedCommand);

            return parsedCommand;
        }
        
        /// <summary>
        /// Parse redirection operators and arguments from the token list.
        /// </summary>
        private static void ParseRedirectionAndArguments(List<string> argList, ParsedCommand parsedCommand)
        {
            var redirState = RedirState.None;

            for (var i = 1; i < argList.Count; i++)
            {
                var arg = argList[i];
                
                // Handle redirection operators
                redirState = arg switch
                {
                    ">" or "1>" => RedirState.RedirectOutput,
                    "2>" => RedirState.RedirectError,
                    ">>" or "1>>" => RedirState.AppendOutput,
                    "2>>" => RedirState.AppendError,
                    _ => redirState
                };
                
                // Skip redirection operators
                if (redirState != RedirState.None && (arg.StartsWith('>') || arg.EndsWith('>')))
                    continue;

                // Handle the argument based on the current redirection state
                switch (redirState)
                {
                    case RedirState.RedirectOutput or RedirState.AppendOutput:
                        parsedCommand.OutputFile = arg;
                        parsedCommand.AppendOutput = redirState == RedirState.AppendOutput;
                        redirState = RedirState.None; // Reset after handling
                        break;
                    case RedirState.RedirectError or RedirState.AppendError:
                        parsedCommand.ErrorFile = arg;
                        parsedCommand.AppendError = redirState == RedirState.AppendError;
                        redirState = RedirState.None; // Reset after handling
                        break;
                    default:
                        parsedCommand.Args.Add(arg);
                        break;
                }
            }
        }

        /// <summary>
        /// Handles output redirection to a file, either by appending or overwriting.
        /// Uses async I/O but doesn't block the caller.
        /// </summary>
        public static void HandleRedirection(string? content, string filePath, bool append)
        {
            if (string.IsNullOrEmpty(filePath))
                return;
                
            var text = content ?? string.Empty;
            
            // Perform path expansion for ~/ paths (Linux feature)
            filePath = ExpandPath(filePath);

            // Fire and forget with proper error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    if (append)
                        await File.AppendAllTextAsync(filePath, text);
                    else
                        await File.WriteAllTextAsync(filePath, text);
                }
                catch (Exception ex)
                {
                    // In a real shell, we'd report this to stderr
                    // But we follow the existing silent error handling pattern
                    await Console.Error.WriteLineAsync($"Error writing to file {filePath}: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// Expands a path with tilde (~) to an absolute path using the user's home directory.
        /// </summary>
        private static string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
                
            if (path == "~" && HomeDirectory != null)
                return HomeDirectory;
                
            if (path.StartsWith("~/") && HomeDirectory != null)
                return Path.Join(HomeDirectory, path[2..]);
                
            return path;
        }
    }
}