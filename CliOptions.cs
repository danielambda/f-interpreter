using CommandLine;
using LanguageExt;

namespace FCompiler;

[Verb("run", HelpText = "Execute a program file")]
public class InterpretOpts {
    [Option("dump-optimized-ast", Required = false,
        HelpText = "Dump optimized AST before executing")]
    public bool DumpOptimizedAst { get; init; } = false;

    [Option("no-optimize", Required = false,
        HelpText = "Disable optimizations")]
    public bool DisableOptimizations { get; init; } = false;

    [Option("include", Separator = ' ',
        HelpText = "Files to evaluate before main file interpretation.")]
    public IEnumerable<string> FilenamesToInclude { get; init; } = [];

    [Value(0, MetaName = "filename", Required = true,
        HelpText = "The source file to execute.")]
    public required string Filename { get; init; }
}

[Verb("repl", HelpText = "Launch interactive REPL")]
public class ReplOpts {
    [Option("files", Separator = ' ',
        HelpText = "Files to evaluate before entering the REPL.")]
    public IEnumerable<string> Filenames { get; init; } = [];
}

public static class CliOptions {
    public static Either<List<Error>, Either<InterpretOpts, ReplOpts>> Parse(string[] args) {
        var parser = new CommandLine.Parser(with => {
            with.HelpWriter = null;
            with.AutoHelp = false;
            with.AutoVersion = false;
        });
        return parser
            .ParseArguments<InterpretOpts, ReplOpts>(PrependRun(args))
            .MapResult<InterpretOpts, ReplOpts, Either<List<Error>, Either<InterpretOpts, ReplOpts>>>(
                (InterpretOpts opts) => (Either<InterpretOpts, ReplOpts>)opts,
                (ReplOpts      opts) => (Either<InterpretOpts, ReplOpts>)opts,
                errs => errs.ToList()
            );
    }

    private static string[] PrependRun(string[] args)
        => args is [var arg0, ..]
        && arg0 is { Length: >= 1 } and not ("run" or "repl")
            ? ["run", ..args]
            : args;
}
