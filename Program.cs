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
        (func inc (x) (plus x 1))
        (prog (result)
          (setq result (inc 5))
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
    new
    {
        Name = "Пример 7: Рекурсивные вычисления",
        Code = """
        (func factorial (n)
          (cond (equal n 0)
            1
            (times n (factorial (minus n 1)))))

        (prog (result)
          (setq result (factorial 5))
          result)
        """,
        ExpectedOptimization = "Должен попытаться инлайнить factorial (но может не сработать из-за рекурсии)"
    },
    new
    {
        Name = "Пример 8: Lambda-функции",
        Code = """
        (prog (result)
          (setq result ((lambda (x y) (times (plus x y) 2)) 3 4))
          result)
        """,
        ExpectedOptimization = "Должен вычислить (3+4)*2=14"
    },
    new
    {
        Name = "Пример 9: Множественные неиспользуемые переменные",
        Code = """
        (prog (used1 unused1 used2 unused2 unused3)
          (setq used1 1)
          (setq unused1 2)
          (setq used2 3)
          (setq unused2 4)
          (setq unused3 5)
          (plus used1 used2))
        """,
        ExpectedOptimization = "Должен оставить только used1 и used2"
    },
    new
    {
        Name = "Пример 10: Оптимизация в цикле",
        Code = """
        (func calculate (x) (plus x 1))
        (prog (i sum temp)
          (setq i 0)
          (setq sum 0)
          (setq temp 999)  ; не используется в цикле
          (while (less i 5)
            (prog ()
              (setq sum (calculate sum))
              (setq i (plus i 1))))
          sum)
        """,
        ExpectedOptimization = "Должен удалить temp и инлайнить calculate"
    }
};

foreach (var example in examples)
{
    Console.WriteLine($"\n{new string('=', 60)}");
    Console.WriteLine(example.Name);
    Console.WriteLine($"Ожидаемая оптимизация: {example.ExpectedOptimization}");
    Console.WriteLine(new string('=', 60));

    try
    {
        var tokens = Lexer.Lex(example.Code.Split('\n')).Sequence();
        var ast = tokens.Match(
            Left: a => throw new Exception($"Lexer error: {a}"),
            Right: ts => new Parser(ts.ToArray()).ParseProgram()
        );

        var semantics = ast.Match(
            Left: a => throw new Exception($"Parser error: {a}"),
            Right: parsedAst => new SemanticAnalyzer().Analyze(parsedAst)
        );

        semantics.Match(
            Left: errors => {
                foreach (var error in errors)
                    Console.WriteLine($"Semantic error: {error.message} at {error.span}");
                throw new Exception("Semantic analysis failed");
            },
            Right: _ => Console.WriteLine("✓ Семантический анализ пройден")
        );

        var optimized = ast.Match(
            Left: a => throw new Exception($"AST error: {a}"),
            Right: parsedAst => new Optimizer(parsedAst).Optimize()
        );

        Console.WriteLine("✓ Оптимизация завершена успешно!");

        // Сравнение исходного и оптимизированного AST
        ast.Match(
            Left: _ => {},
            Right: original =>
            {
                Console.WriteLine("\nИСХОДНЫЙ AST:");
                Console.WriteLine(original.PrettyPrint());
                Console.WriteLine("\nОПТИМИЗИРОВАННЫЙ AST:");
                Console.WriteLine(optimized.PrettyPrint());

                // Простая проверка изменений
                var originalStr = original.PrettyPrint();
                var optimizedStr = optimized.PrettyPrint();

                if (originalStr == optimizedStr)
                {
                    Console.WriteLine("\n⚠️  AST не изменился после оптимизации");
                }
                else
                {
                    Console.WriteLine("\n✅ AST был изменен оптимизатором");
                }
            }
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Ошибка: {ex.Message}");
    }
}

Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("ТЕСТИРОВАНИЕ ЗАВЕРШЕНО");
Console.WriteLine(new string('=', 60));
