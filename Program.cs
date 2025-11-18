using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using FCompiler.Optimizations;
using LanguageExt;

// Тестовые примеры
var examples = new[]
{
    new
    {
        Name = "Пример 1: Удаление неиспользуемых переменных",
        Code = """
        (prog (a b c)
          (setq a 1)
          (setq b 2)  ; не используется
          (setq c 3)
          (plus a c))
        """,
        ExpectedOptimization = "Должны удалить setq b"
    },
    new
    {
        Name = "Пример 2: Инлайнинг простой функции",
        Code = """
        (func inc (x) (lambda (y) (plus x y)))
        (prog (result y a)
          (setq a 2)
          (setq y 5)
          (setq result (inc y))
          (setq result (result a))
          result)
        """,
        ExpectedOptimization = "Должен заменить вызов inc на (plus 5 1)"
    },
    new
    {
        Name = "Пример 3: Арифметическая оптимизация констант",
        Code = """
        (prog ()
          (plus 2 3)
          (times 4 5)
          (minus 10 3))
        """,
        ExpectedOptimization = "Должен вычислить 2+3=5, 4*5=20, 10-3=7"
    },
    new
    {
        Name = "Пример 4: Комбинированная оптимизация",
        Code = """
        (func double (x) (times x 2))
        (prog (a b unused)
          (setq a 5)
          (setq b 10)
          (setq unused 99)  ; не используется
          (double (plus a b)))
        """,
        ExpectedOptimization = "Должен инлайнить double и удалить unused"
    },
    new
    {
        Name = "Пример 5: Вложенные вызовы функций",
        Code = """
        (func add (x y) (plus x y))
        (func square (x) (times x x))
        (prog (result)
          (setq result (square (add 3 4)))
          result)
        """,
        ExpectedOptimization = "Должен инлайнить add и square, вычислить 3+4=7, 7*7=49"
    },
    new
    {
        Name = "Пример 6: Сложное условие с неиспользуемыми переменными",
        Code = """
        (prog (x y z temp1 temp2)
          (setq x 10)
          (setq y 20)
          (setq z 30)
          (setq temp1 100)  ; не используется
          (setq temp2 200)  ; не используется
          (cond (less x y)
            (plus x z)
            (plus y z)))
        """,
        ExpectedOptimization = "Должен удалить temp1 и temp2"
    },
};

foreach (var example in examples.Skip(1).Take(1)) {
    Console.WriteLine($"\n{new string('=', 60)}");
    Console.WriteLine(example.Name);
    Console.WriteLine($"Ожидаемая оптимизация: {example.ExpectedOptimization}");
    Console.WriteLine(new string('=', 60));

    var tokens = Lexer.Lex(example.Code.Split('\n')).Sequence();
    var ast = tokens.Match(
        Left: a => throw new Exception($"Lexer error: {a}"),
        Right: ts => new Parser(ts.ToArray()).ParseProgram()
    );

    var semAst = ast.Match(
        Left: a => throw new Exception($"Parser error: {a}"),
        Right: parsedAst => Analyzer.Analyze(parsedAst)
    );

    var optimized = semAst.Match(
        Left: error => {
            Console.WriteLine($"Semantic error: {error.message} at {error.span}");
            throw new Exception("Semantic analysis failed");
        },
        Right: semAst => {
            Console.WriteLine("✓ Семантический анализ пройден");
            return Optimizer.Optimize(semAst);
        }
    );

    Console.WriteLine("✓ Оптимизация завершена успешно!");

    // Сравнение исходного и оптимизированного AST
    semAst.Match(
        Left: _ => {},
        Right: original => {
            var originalStr = original.ToAst().PrettyPrint();
            var optimizedStr = optimized.ToAst().PrettyPrint();

            Console.WriteLine("\nИСХОДНЫЙ AST:");
            Console.WriteLine(originalStr);
            Console.WriteLine("\nОПТИМИЗИРОВАННЫЙ AST:");
            Console.WriteLine(optimizedStr);

            if (originalStr == optimizedStr)
                Console.WriteLine("\n⚠️  AST не изменился после оптимизации");
            else
                Console.WriteLine("\n✅ AST был изменен оптимизатором");
        }
    );
}

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("ТЕСТИРОВАНИЕ ЗАВЕРШЕНО");
Console.WriteLine(new string('=', 60));
