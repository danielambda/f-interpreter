using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using FCompiler.Interpreter;
using LanguageExt;

// Тестовые примеры для интерпретатора
var examples = new[]
{
    new
    {
        Name = "Пример 1: Простая арифметика",
        Code = """
        (plus 1 2)
        """,
        Description = "Сложение двух чисел"
    },
    new
    {
        Name = "Пример 2: Условное выражение",
        Code = """
        (cond true 1 2)
        """,
        Description = "Условное выражение с истинным условием"
    },
    new
    {
        Name = "Пример 3: Работа с переменными",
        Code = """
        (prog (x y)
          (setq x 10)
          (setq y 20)
          (plus x y))
        """,
        Description = "Объявление переменных и их использование"
    },
    new
    {
        Name = "Пример 4: Функция и цикл",
        Code = """
        (func factorial (n)
          (prog (result i)
            (setq result 1)
            (setq i 1)
            (while (lesseq i n)
              (prog ()
                (setq result (times result i))
                (setq i (plus i 1))))
            result))
        
        (factorial 5)
        """,
        Description = "Вычисление факториала с помощью функции и цикла"
    },
    new
    {
        Name = "Пример 5: Работа со списками",
        Code = """
        (prog (lst)
          (setq lst (cons 1 (cons 2 (cons 3 null))))
          (head (tail lst)))
        """,
        Description = "Создание списка и операции head/tail"
    },
    new
    {
        Name = "Пример 6: Лямбда-функции",
        Code = """
        (prog (adder)
          (setq adder (lambda (x) (lambda (y) (plus x y))))
          ((adder 5) 3))
        """,
        Description = "Использование лямбда-функций для замыканий"
    },
    new
    {
        Name = "Пример 7: Логические операции",
        Code = """
        (and (less 1 2) (greater 5 3))
        """,
        Description = "Логическая операция AND"
    },
    new
    {
        Name = "Пример 8: Сравнения",
        Code = """
        (prog (a b)
          (setq a 10)
          (setq b 15)
          (less a b))
        """,
        Description = "Сравнение двух переменных"
    },
    new 
    {
        Name = "Пример 9",
        Code = """
        (func sqrt-newton (x tol)        
            (prog (guess)        
                (setq guess (div x 2.0))        
                    (while true        
                        (prog (new-guess)        
                            (setq new-guess (div (plus guess (div x guess)) 2.0))        
                                (cond (less (abs (minus new-guess guess)) tol)        
                                    (return new-guess)        
                                    (setq guess new-guess))))))        

        (sqrt-newton 2.0 0.001)
        """,
        Description = ""
      
    }
};

var interpreter = new Interpreter();

Console.WriteLine("🚀 ЗАПУСК ИНТЕРПРЕТАТОРА ЯЗЫКА F");
Console.WriteLine(new string('=', 70));

foreach (var example in examples)
{
    Console.WriteLine($"\n📋 {example.Name}");
    Console.WriteLine($"📝 {example.Description}");
    Console.WriteLine(new string('-', 50));
    Console.WriteLine("Исходный код:");
    Console.WriteLine(example.Code);
    Console.WriteLine(new string('-', 50));

    try
    {
        // Лексический анализ
        var tokens = Lexer.Lex(example.Code.Split('\n')).Sequence();
        var ast = tokens.Match(
            Left: a => throw new Exception($"Ошибка лексического анализа: {a}"),
            Right: ts => new Parser(ts.ToArray()).ParseProgram()
        );

        // Синтаксический анализ
        var semAst = ast.Match(
            Left: a => throw new Exception($"Ошибка синтаксического анализа: {a}"),
            Right: parsedAst => Analyzer.Analyze(parsedAst)
        );

        // Интерпретация
        semAst.Match(
            Left: error => Console.WriteLine($"❌ Семантическая ошибка: {error.message}"),
            Right: semAst =>
            {
                Console.WriteLine("✅ Семантический анализ пройден");
                
                // Выполнение программы
                var result = interpreter.Interpret(semAst);
                
                // Красивый вывод результата
                Console.WriteLine("📊 РЕЗУЛЬТАТ ВЫПОЛНЕНИЯ:");
                PrintValue(result, 0);
            }
        );
    }
    catch (Exception e)
    {
        Console.WriteLine($"💥 Ошибка выполнения: {e.Message}");
        if (e.InnerException != null)
        {
            Console.WriteLine($"   Внутренняя ошибка: {e.InnerException.Message}");
        }
    }
    
    Console.WriteLine(new string('=', 70));
}

Console.WriteLine("\n🎉 ВСЕ ПРИМЕРЫ ВЫПОЛНЕНЫ!");

// Метод для красивого вывода значений
void PrintValue(Value value, int indent)
{
    var indentStr = new string(' ', indent * 2);
    
    switch (value)
    {
        case Value.Integer i:
            Console.WriteLine($"{indentStr}🔢 Целое число: {i.Value}");
            break;
        case Value.Real r:
            Console.WriteLine($"{indentStr}🔢 Вещественное число: {r.Value}");
            break;
        case Value.Bool b:
            Console.WriteLine($"{indentStr}✅ Логическое значение: {(b.Value ? "true" : "false")}");
            break;
        case Value.Null:
            Console.WriteLine($"{indentStr}⚫ Null");
            break;
        case Value.List list:
            Console.WriteLine($"{indentStr}📋 Список [{list.Values.Count} элементов]:");
            if (list.Values.Count == 0)
            {
                Console.WriteLine($"{indentStr}  (пустой список)");
            }
            else
            {
                for (int i = 0; i < list.Values.Count; i++)
                {
                    Console.Write($"{indentStr}  [{i}]: ");
                    PrintValue(list.Values[i], 0);
                }
            }
            break;
        case Value.Function func:
            Console.WriteLine($"{indentStr}🔧 Пользовательская функция");
            Console.WriteLine($"{indentStr}  Параметры: ({string.Join(", ", func.Parameters)})");
            break;
        case Value.BuiltinFunction builtin:
            Console.WriteLine($"{indentStr}⚙️  Встроенная функция: {builtin.Name}");
            break;
        default:
            Console.WriteLine($"{indentStr}❓ Неизвестный тип значения: {value.GetType().Name}");
            break;
    }
}