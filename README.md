[![progress-banner](https://backend.codecrafters.io/progress/shell/3b20ac08-eabc-405b-b3b6-57f723c5e686)](https://app.codecrafters.io/users/codecrafters-bot?r=2qF)

This is my C# solutions to the
["Build Your Own Shell" Challenge](https://app.codecrafters.io/courses/shell/overview).

A minimal POSIX-style shell written in C# (.NET 9). It supports built-ins (cd, pwd, echo, type, history), running external programs, simple pipelines, quoting/escaping, and basic output redirection. Includes command auto-completion and async I/O for a responsive experience.


**Note**: Head over to
[codecrafters.io](https://codecrafters.io) to try the challenge.

## Features

- Built-in commands:
    - echo, type, pwd, cd, history (with -r, -w, -a)
- Run external commands found on PATH
- Pipelines: cmd1 | cmd2 | cmd3
- Quoting and escaping: single quotes, double quotes, backslashes
- Basic redirection operators in non-pipeline mode:
    - stdout: >, >>, 1>, 1>>
    - stderr: 2>, 2>>
- Command auto-completion (built-ins and executables in PATH)
- Async file I/O for history and redirections

## Requirements

- .NET SDK 9.0 (or compatible runtime)
- Linux environment (uses Unix file permissions and behavior)

## Usage Examples

- Built-ins:
    - Echo: `echo hello world`
    - Print working directory: `pwd`
    - Change directory: `cd /tmp` or `cd ~`
    - Type info: `type echo`, `type ls`
    - History:
        - Show last N: `history 5`
        - Read from file: `history -r /path/to/file`
        - Write to file: `history -w /path/to/file`
        - Append to file: `history -a /path/to/file`

- External commands:
    - `ls -la`
    - `grep main *.cs`

- Pipelines:
    - `ls -1 | head`
    - `cat README.md | grep shell`

- Redirection (non-pipeline):
    - `echo hi > out.txt`
    - `echo hi >> out.txt`
    - `somecmd 2> errors.log`
    - `somecmd 1> out.log 2>> errors.log`

## Notes and Limitations

- Linux-focused: command resolution uses Unix execute permissions.
- Pipelines are supported for streaming between stages.
- Output redirection for pipelines is limited: final-stage output is printed to the console; file redirection for pipeline output is not currently supported.
- Quoting/escaping is implemented to cover common cases (single/double quotes and backslashes).

## Project Structure

- src/main.cs — REPL entry point
- src/Shell.cs — built-ins, external command execution, history
- src/PipelineExecutor.cs — pipeline orchestration
- src/AutoCompleteHandler.cs — tab completion
- src/ShellHelpers.cs — parsing, command resolution, redirection utilities

## Development

- Format and lint via your IDE or editor of choice.
- Target framework: net9.0
- Language version: C# 13

## License

MIT (or the license specified by the repository).
