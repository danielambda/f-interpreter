using FCompiler.Lexer;

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

foreach (var token in Lexer.Lex(example))
  Console.WriteLine(token);
