using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using FCompiler.Interpreter;
using LanguageExt;

var examples = new[]
{
    new
    {
        Name = "Example 1",
        Code = """
        (quote (plus 1 2))
        """
    },
    new
    {
        Name = "Example 2", 
        Code = """
        (setq x 10)
        (eval 'x)
        """
    },
    new
    {
        Name = "Example 3",
        Code = """
        (func sqrtNewton (x tol)
            (prog (guess)
                (setq guess (divide x 2.0))
                    (while true
                        (prog (newGuess)
                            (setq newGuess (divide (plus guess (divide x guess)) 2.0))
                                (cond (less (abs (minus newGuess guess)) tol)
                                    (return newGuess)
                                    (setq guess newGuess))))))

        (sqrtNewton 2 0.001)
        """
    }
};

var interpreter = new Interpreter();

foreach (var example in examples)
{
    Console.WriteLine($"{example.Name}");
    Console.WriteLine("Code:");
    Console.WriteLine(example.Code);
    Console.WriteLine("Result:");

    try
    {
        var tokens = Lexer.Lex(example.Code.Split('\n')).Sequence();
        var ast = tokens.Match(
            Left: a => throw new Exception($"Lexer error: {a}"),
            Right: ts => new Parser(ts.ToArray()).ParseProgram()
        );

        var semAst = ast.Match(
            Left: a => throw new Exception($"Parser error: {a}"),
            Right: parsedAst => Analyzer.Analyze(parsedAst)
        );

        semAst.Match(
            Left: error => Console.WriteLine($"Semantic error: {error.message}"),
            Right: semAst =>
            {
                var result = interpreter.Interpret(semAst);
                PrintValue(result);
            }
        );
    }
    catch (Exception e)
    {
        Console.WriteLine($"Error: {e.Message}");
    }

    Console.WriteLine();
}

void PrintValue(Value value)
{
    switch (value)
    {
        case Value.Atom a:
            Console.WriteLine($"Atom: {a.Name}");
            break;
        case Value.Integer i:
            Console.WriteLine($"Integer: {i.Value}");
            break;
        case Value.Real r:
            Console.WriteLine($"Real: {r.Value}");
            break;
        case Value.Bool b:
            Console.WriteLine($"Bool: {b.Value}");
            break;
        case Value.Null:
            Console.WriteLine("Null");
            break;
        case Value.List list:
            Console.WriteLine($"List [{list.Values.Count}]:");
            foreach (var item in list.Values)
            {
                Console.Write("  ");
                PrintValue(item);
            }
            break;
        case Value.Function func:
            Console.WriteLine($"Function: ({string.Join(", ", func.Parameters)})");
            break;
        case Value.BuiltinFunction builtin:
            Console.WriteLine($"Builtin: {builtin.Name}");
            break;
        default:
            Console.WriteLine($"Unknown: {value.GetType().Name}");
            break;
    }
}