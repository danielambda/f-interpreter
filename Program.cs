using FCompiler.Interpreter;
using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Printing;
using FCompiler.Semantic;
using FCompiler;
using LanguageExt;
using static LanguageExt.Prelude;

CliOptions.Parse(args).Match(
    Left: errs => {
        Console.WriteLine("Could not parse CLI args");
        errs.ForEach(Console.WriteLine);
    },
    Right: opts => {
        try {
            opts.Match(
                Left: RunInterpret,
                Right: RunRepl
            );
        } catch (Exception exception) {
            Console.WriteLine(exception.Message);
        }
    }
);

void RunRepl(ReplOpts opts) {
    var interpreter = new Interpreter();

    foreach (var file in opts.Filenames) LoadFile(file);

    List<string> lines = [];
    Console.Write(">>> ");
    var line = Console.ReadLine();

    while (true) {
        try {
            if (line is null) continue;
            line = line.TrimStart();

            if (lines.All(string.IsNullOrWhiteSpace) && line.StartsWith(":load")) {
                var fileToLoad = line[5..].Trim();
                LoadFile(fileToLoad);
            } else {
                lines.Add(line);
                var tokens = Lexer.Lex(lines).Sequence();
                var ast = tokens.Match(
                    Left: error => throw new Exception($"Lexer error: {error}"),
                    Right: Parser.Parse
                );

                var semAstM = ast.Match(
                    Left: error => {
                        if (error is not (ParserError.ExpectedRParen or ParserError.NoTokens)) {
                            throw new Exception($"Parser error: {error}");
                        }
                    },
                    Right: parsedAst => {
                        var semAst = Analyzer.Analyze(parsedAst);
                        var resultValues = semAst.Match(
                            Left: error => throw new Exception($"Semantic error: {error.message}"),
                            Right: interpreter.Interpret
                        );

                        var results = resultValues.Map(LispPrinter.FormatValue).Where(v => v is not null);
                        foreach (var res in results) {
                            Console.WriteLine(res);
                        }
                        lines.Clear();
                    }
                );
            }

            Console.Write(">>> ");
            line = Console.ReadLine();
        } catch (Exception exception) {
            Console.WriteLine(exception.Message);
            lines.Clear();
            Console.Write(">>> ");
            line = Console.ReadLine();
        }
    }

    void LoadFile(string fileToLoad) {
        Console.WriteLine("loading file " + fileToLoad);
        try {
            var fileContents = File.ReadLines(fileToLoad);

            var resultsE =
                from tokens in Lexer.Lex(fileContents).Sequence()
                    .MapLeft(e => WrapInException("Lexer",    e))
                from ast    in Parser.Parse(tokens)
                    .MapLeft(e => WrapInException("Parser",   e))
                from semAst in Analyzer.Analyze(ast)
                    .MapLeft(e => WrapInException("Analyzer", e))
                select interpreter.Interpret(semAst).Map(LispPrinter.FormatValue);

            var results = resultsE.Match(
                Left: expetion => throw expetion,
                Right: results => results
            );

            foreach (var res in results.Where(r => r is not null)) {
                Console.WriteLine(res);
            }
        } catch (Exception expeption) {
            Console.WriteLine($"Error while loading file {fileToLoad}");
            Console.WriteLine(expeption.Message);
        }
        Console.WriteLine();
    }
}

void RunInterpret(InterpretOpts opts) {
    var lines = File.ReadLines(opts.Filename);

    try {
        var interpreter = new Interpreter();

        var tokens = Lexer.Lex(lines).Sequence();
        var ast = tokens.Match(
            Left: error => throw new Exception($"Lexer error: {error}"),
            Right: Parser.Parse
        );

        var semAst = ast.Match(
            Left: error => throw new Exception($"Parser error: {error}"),
            Right: Analyzer.Analyze
        );

        var resultValues = semAst.Match(
            Left: error => throw new Exception($"Semantic error: {error.message}"),
            Right: interpreter.Interpret
        );

        var results = resultValues.Map(LispPrinter.FormatValue).Where(v => v is not null);
        foreach (var res in results) {
            Console.WriteLine(res);
        }
    } catch (Exception e) {
        Console.WriteLine($"Error: {e.Message}");
    }
}

Exception WrapInException<T>(string component, T t) => new Exception($"{component} error: {t}");
