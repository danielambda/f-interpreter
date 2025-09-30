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

var lexer = new Lexer();
var lines = example.Split('\n');

foreach (var line in lines)
{
    lexer.SetInput(line);
    foreach (var token in lexer.Tokens())
    {
        Console.WriteLine(token);
    }
}