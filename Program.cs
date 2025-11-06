using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using LanguageExt;

var example = """
(prog (i sum)
  '(setq i 0)
  (setq sum 0)
  (while '(true)
    (prog ()
      (setq sum (plus sum i))
      (setq i (plus i 1))
      (cond (equal i 5) break)))
  sum) ; 10
""";

var tokens = Lexer.Lex(example.Split('\n')).Sequence();
var ast = tokens.Match(
  Left: a => throw new Exception(a.ToString()),
  Right: ts => new Parser(ts.ToArray()).ParseProgram()
);
var semantics = ast.Match(
  Left: a => throw new Exception(a.ToString()),
  Right: aboba => new SemanticAnalyzer().Analyze(aboba)
);
semantics.Match(
  Left: aboba => aboba.Select(e => e.message).ToList().ForEach(Console.WriteLine),
  Right: _ => Console.WriteLine("sall good man")
);

