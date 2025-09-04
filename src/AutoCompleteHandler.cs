using ReadLine;

namespace cc_shell
{
    /// <summary>
    /// Provides tab-completion suggestions for the shell's ReadLine input.
    /// It suggests built-in commands and external executables found in the system's PATH.
    /// </summary>
    internal class AutoCompleteHandler : IAutoCompleteHandler
    {
        public char[] Separators { get; set; } = [' ', '.', '/'];

        public string[] GetSuggestions(string text, int index)
        {
            var suggestions = new List<string>();

            // Get suggestions from both built-in commands and external executables.
            suggestions.AddRange(GetBuiltInSuggestions(text));
            suggestions.AddRange(GetPathSuggestions(text));

            if (suggestions.Count == 0)
            {
                Console.Write("\a"); // Send a bell character to the terminal to indicate an error.
                return [];
            }

            if (suggestions.Count == 1)
                return [suggestions[0]];

            var commonPrefix = FindLongestCommonPrefix(suggestions);

            // If we found a common prefix that's longer than what the user has already typed,
            if (string.IsNullOrEmpty(commonPrefix) == false && commonPrefix.Length > text.Length)
                return [commonPrefix];

            // 3. If there's no common prefix beyond what's typed, return all possibilities.
            return suggestions.ToArray();
        }

        /// <summary>
        /// Finds built-in shell commands that start with the given text.
        /// </summary>
        private static IEnumerable<string> GetBuiltInSuggestions(string text)
        {
            return Shell.BuiltinCommands
                .Where(cmd => cmd.StartsWith(text))
                .Select(cmd => cmd + " "); // Add a space for convenience.
        }

        /// <summary>
        /// Finds executable files in the system's PATH that start with the given text.
        /// </summary>
        private static HashSet<string> GetPathSuggestions(string text)
        {
            // Use a HashSet to automatically handle duplicates
            var suggestions = new HashSet<string>();

            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathDirectories = pathVariable.Split(Path.PathSeparator);

            // Loop through each directory listed in the PATH environment variable.
            foreach (var directory in pathDirectories)
            {
                if (Directory.Exists(directory) == false)
                    continue;

                try
                {
                    foreach (var filePath in Directory.GetFiles(directory))
                    {
                        var fileName = Path.GetFileName(filePath);
                        if (fileName.StartsWith(text) && ShellHelpers.HasExecutePermission(filePath))
                            suggestions.Add(fileName + " ");
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore directories we don't have permission to read
                }
            }

            return suggestions;
        }

        /// <summary>
        /// Finds the longest common starting sequence of characters among a list of strings.
        /// </summary>
        /// <param name="strings">A list of strings to compare.</param>
        /// <returns>The longest common prefix.</returns>
        private static string FindLongestCommonPrefix(List<string> strings)
        {
            if (strings.Count == 0)
                return string.Empty;

            if (strings.Count == 1)
                return strings[0];

            // A standard and efficient algorithm for finding the common prefix is to compare just the first and last strings of a sorted set.
            strings.Sort();
            var first = strings[0];
            var last = strings[^1];

            var prefixLength = 0;
            while (prefixLength < first.Length && prefixLength < last.Length && first[prefixLength] == last[prefixLength])
            {
                prefixLength++;
            }

            return first[..prefixLength];
        }
    }
}