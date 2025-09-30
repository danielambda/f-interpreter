using FCompiler.Lexer;
using FCompiler.Parser;
using static LanguageExt.Prelude;

var example = """
(prog (i sum)
  (setq i 0)
  (setq sum 0)
  (while (true)
    (prog ()
      (setq sum (plus sum i))
      (setq i (plus i 1))
      (cond (equal i 5) break)))
  sum) ; 10
""";

var tokens = Lexer.Lex(example.Split('\n')).Sequence();
tokens.Match(
  Left: Console.WriteLine,
  Right: ts => Console.WriteLine(new Parser(ts.ToArray()).ParseProgram())
);
