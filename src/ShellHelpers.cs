using System.Runtime.CompilerServices;
using System.Text;

namespace cc_shell
{
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

        public static bool HasExecutePermission(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var unixFileMode = fileInfo.UnixFileMode;
            return (unixFileMode & UnixFileMode.UserExecute) != 0;
        }

        public static bool TryGetCommandDir(string command, out string? fullPath)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            var pathDirectories = path?.Split(Path.PathSeparator);

            fullPath = null;

            if (pathDirectories == null)
                return false;

            foreach (var directory in pathDirectories)
            {
                fullPath = Path.Join(directory, command);
                if (File.Exists(fullPath) == false || HasExecutePermission(fullPath) == false)
                    continue;

                return true;
            }

            return false;
        }

        // Returns true if the character was fully handled (toggle/escape) and should NOT be appended by the caller.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HandleQuoteAndEscape(char c, ref bool openSingleQuote, ref bool openDoubleQuote, ref bool backSlash,
            StringBuilder appendTarget)
        {
            // Cache flags to avoid repeated loads and enable tighter branching
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


        // Split the full command line into pipeline segments separated by '|'
        public static List<string> SplitPipelineSegments(string text)
        {
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

            if (current.Length > 0)
                segments.Add(current.ToString().Trim());

            segments.RemoveAll(string.IsNullOrWhiteSpace);
            return segments;
        }

        public static ParsedCommand ParseConsoleText(string text)
        {
            var resultBuilder = new StringBuilder();
            var openSingleQuote = false;
            var openDoubleQuote = false;
            var backSlash = false;
            var argList = new List<string>();

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

            if (resultBuilder.Length > 0)
                argList.Add(resultBuilder.ToString());

            var parsedCommand = new ParsedCommand
            {
                Command = argList[0]
            };

            var redirState = RedirState.None;

            for (var i = 1; i < argList.Count; i++)
            {
                var arg = argList[i];
                switch (arg)
                {
                    case ">":
                    case "1>":
                        redirState = RedirState.RedirectOutput;
                        continue;
                    case "2>":
                        redirState = RedirState.RedirectError;
                        continue;
                    case ">>":
                    case "1>>":
                        redirState = RedirState.AppendOutput;
                        continue;
                    case "2>>":
                        redirState = RedirState.AppendError;
                        continue;
                }

                switch (redirState)
                {
                    case RedirState.RedirectOutput or RedirState.AppendOutput:
                        parsedCommand.OutputFile = arg;
                        parsedCommand.AppendOutput = redirState == RedirState.AppendOutput;
                        break;
                    case RedirState.RedirectError or RedirState.AppendError:
                        parsedCommand.ErrorFile = arg;
                        parsedCommand.AppendError = redirState == RedirState.AppendError;
                        break;
                    default:
                        parsedCommand.Args.Add(arg);
                        break;
                }
            }

            return parsedCommand;
        }

        public static void HandleRedirection(string? content, string filePath, bool append)
        {
            if (append)
                File.AppendAllTextAsync(filePath, content);
            else
                File.WriteAllTextAsync(filePath, content);
        }
    }
}