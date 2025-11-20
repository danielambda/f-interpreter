using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using FCompiler.Interpreter;
using LanguageExt;

if (args is ["-i"]) {
    Console.WriteLine("Running in interactive mode");
    var interpreter = new Interpreter();

    List<string> lines = [];
    var line = Console.ReadLine();

    while (true) {
        if (line is null) continue;
        line = line.TrimStart();

        if (string.IsNullOrWhiteSpace(line)) {
            var results = Interpret(interpreter, lines);
            lines.Clear();

            foreach (var res in results.Where(r => r is not null))
                Console.WriteLine(res);
        } else if (lines.All(string.IsNullOrWhiteSpace) && line.StartsWith(":load")) {
            var fileToLoad = line[5..].Trim();
            Console.WriteLine("loading file " + fileToLoad);
            var fileContents = File.ReadLines(fileToLoad);

            var results = Interpret(interpreter, lines);

            foreach (var res in results.Where(r => r is not null))
                Console.WriteLine(res);
        } else {
            lines.Add(line);
        }

        line = Console.ReadLine();
    }
}

if (args is ["-f", var filePath]) {
    var lines = File.ReadLines(filePath);

    try {
        var interpreter = new Interpreter();

        var results = Interpret(interpreter, lines);

        foreach (var res in results.Where(r => r is not null))
            Console.WriteLine(res);
    } catch (Exception e) {
        Console.WriteLine($"Error: {e.Message}");
    }

    Console.WriteLine();
    return;
}

var examples = new[] {
    new {
        Name = "Example 1",
        Code = """
        (quote (plus 1 2))
        """
    },
    new {
        Name = "Example 2",
        Code = """
        (setq x 10)
        (eval 'x)
        """
    },
    new {
        Name = "Example 3",
        Code = """
        (func sqrtNewton (x tol)
            (prog (guess)
                (setq guess (divide x 2.0))
                (while true (prog (newGuess)
                    (setq newGuess (divide (plus guess (divide x guess)) 2.0))
                        (cond (less (abs (minus newGuess guess)) tol)
                            (return newGuess)
                            (setq guess newGuess))))))

        (sqrtNewton 2 0.001)
        """
    },
    new {
        Name = "Example 4",
        Code = """
        '()

        (func map (f lst)
          (cond (equal lst '())
            '()
            (cons (f (head lst)) (map f (tail lst)))))

        (func id (x) x)

        (func curry (f)
          (lambda (x) (lambda (y) (f x y))))

        (setq curriedMap (curry map))

        (map ((curry plus) 2) '(1 2 3 -2))
        """
    },
};

foreach (var example in examples) {
    var interpreter = new Interpreter();

    Console.WriteLine($"{example.Name}");
    Console.WriteLine("Code:");
    Console.WriteLine(example.Code);
    Console.WriteLine("Result:");

    try {
        var tokens = Lexer.Lex(example.Code.Split('\n')).Sequence();
        var ast = tokens.Match(
            Left: a => throw new Exception($"Lexer error: {a}"),
            Right: ts => new Parser(ts).ParseProgram()
        );

        var semAst = ast.Match(
            Left: a => throw new Exception($"Parser error: {a}"),
            Right: parsedAst => Analyzer.Analyze(parsedAst)
        );

        var results = semAst.Match(
            Left: error => [$"Semantic error: {error.message}"],
            Right: semAst => interpreter.Interpret(semAst).Select(PrettyPrint)
        );

        foreach (var res in results)
            Console.WriteLine(res);
    } catch (Exception e) {
        Console.WriteLine($"Error: {e.Message}");
    }

    Console.WriteLine();
}

string PrettyPrint(Value value) => value switch {
    Value.Atom a    => $"Atom: {a.Name}",
    Value.Integer i => $"Integer: {i.Value}",
    Value.Real r    => $"Real: {r.Value}",
    Value.Bool b    => $"Bool: {b.Value}",
    Value.Null      => "Null",
    Value.List list => $"List: {string.Join(separator: ' ', list.Values.Select(PrettyPrint))}",
    Value.Function func => $"Function with params: ({string.Join(' ', func.Parameters)})",
    Value.BuiltinFunction builtin => $"Builtin: {builtin.Name}",
    _ => $"Unknown: {value.GetType().Name}",
};

string? LispPrint(Value value) => value switch {
    Value.Atom a    => "'" + a.Name,
    Value.Integer i => i.Value.ToString(),
    Value.Real r    => r.Value.ToString(),
    Value.Bool b    => b.Value.ToString().ToLower(),
    Value.Null      => "null",
    Value.List list => $"'({string.Join(separator: ' ', list.Values.Select(InListLispPrint))})",
    Value.Function func => null,
    Value.BuiltinFunction builtin => null,
    _ => $"Unknown: {value.GetType().Name}",
};

string? InListLispPrint(Value value) => value switch {
    Value.List list => $"({string.Join(separator: ' ', list.Values.Select(InListLispPrint))})",
    Value.Atom a    => a.Name,
    _ => LispPrint(value),
};

IEnumerable<string> Interpret(Interpreter interpreter, IEnumerable<string> lines) {
    var tokens = Lexer.Lex(lines).Sequence();
    var ast = tokens.Match(
        Left: a => throw new Exception($"Lexer error: {a}"),
        Right: ts => new Parser(ts).ParseProgram()
    );

    var semAst = ast.Match(
        Left: a => throw new Exception($"Parser error: {a}"),
        Right: parsedAst => Analyzer.Analyze(parsedAst)
    );

    var results = semAst.Match(
        Left: error => [$"Semantic error: {error.message}"],
        Right: semAst => interpreter.Interpret(semAst).Select(LispPrint)
    );

    return results.Where(r => r is not null)!;
}
