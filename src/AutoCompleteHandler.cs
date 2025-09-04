using ReadLine;

namespace cc_shell
{
    internal class AutoCompleteHandler : IAutoCompleteHandler
    {
        public char[] Separators { get; set; } = [' ', '.', '/'];

        public string[] GetSuggestions(string text, int index)
        {
            if (text.StartsWith("ech"))
                return ["echo "];
            if (text.StartsWith("exi"))
                return ["exit "];
            if (text.StartsWith("typ"))
                return ["type "];
            if (text.StartsWith("his"))
                return ["history "];

            var path = Environment.GetEnvironmentVariable("PATH");
            var pathDirectories = path?.Split(Path.PathSeparator);
            var suggestions = new List<string>();

            if (pathDirectories != null)
            {
                foreach (var directory in pathDirectories)
                {
                    if (Directory.Exists(directory) == false)
                        continue;

                    foreach (var filePath in Directory.GetFiles(directory))
                    {
                        var fileName = Path.GetFileName(filePath);
                        if (fileName.StartsWith(text) && ShellHelpers.HasExecutePermission(filePath))
                        {
                            suggestions.Add(fileName + " ");
                        }
                    }
                }
            }

            if (suggestions.Count == 1)
                return suggestions.ToArray();

            if (suggestions.Count > 1)
            {
                var result = suggestions.ToArray();
                Array.Sort(result);

                for (var i = 0; i < result.Length - 1; i++)
                {
                    var value = result[i].Trim();

                    if (value.StartsWith(text) == false)
                        continue;

                    for (var j = i + 1; j < result.Length; j++)
                    {
                        var compValue = result[j];
                        if (compValue.StartsWith(value))
                            return [value.Trim()];
                    }
                }

                return result;
            }

            Console.Write("\a");
            return null!;
        }
    }
}