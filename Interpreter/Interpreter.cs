using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using FCompiler.Utils;
using static FCompiler.Interpreter.Value;

namespace FCompiler.Interpreter;

public class Interpreter {
    private Environment _globalEnv;

    public Interpreter() {
        _globalEnv = new Environment();
        InitializeBuiltins();
    }

    public IEnumerable<Value> Interpret(Sem.Ast ast) =>
        ast.elements.Map(Interpret);

    public Value Interpret(Sem.Elem elem) => EvaluateElement(elem, _globalEnv);

    private Value EvaluateElement(Sem.Elem elem, Environment env) => elem switch {
        Sem.Setq setq => EvaluateSetq(setq, env),
        Sem.Fun fun => EvaluateFun(fun, env),
        Sem.Lambda lambda => EvaluateLambda(lambda, env),
        Sem.Prog prog => EvaluateProg(prog, env),
        Sem.Cond cond => EvaluateCond(cond, env),
        Sem.While @while => EvaluateWhile(@while, env),
        Sem.Return @return => EvaluateReturn(@return, env),
        Sem.Break => throw new BreakException(),
        Sem.FunApp funApp => EvaluateFunApp(funApp, env),
        Sem.Quote quote => EvaluateQuote(quote),
        Sem.Integer integer => new Integer(integer.integer.integer.value),
        Sem.Real real => new Real(real.real.real.value),
        Sem.Bool @bool => new Bool(@bool.boolean.boolean.value),
        Sem.Null => Null.Instance,
        Sem.Identifier ident => EvaluateIdentifier(ident, env),
        _ => throw new Exception($"Unknown element type: {elem.GetType().Name}")
    };

    private Value EvaluateExpr(Sem.Expr expr, Environment env) =>
        EvaluateElement(expr, env);

    private Value EvaluateSetq(Sem.Setq setq, Environment env) {
        var value = EvaluateExpr(setq.body, env);
        var name = setq.name.identifier.identifier.value;

        if (env.Contains(name)) {
            env.Set(name, value, setq.name.identifier.identifier.span);
        } else {
            env.Define(name, value);
        }

        return value;
    }

    private Value EvaluateFun(Sem.Fun fun, Environment env) {
        var name = fun.name.identifier.identifier.value;
        var paramNames = fun.args.Map(a => a.identifier.identifier.value).ToList();

        var function = new Function(paramNames, fun.body, new Environment(env));
        env.Define(name, function);

        return function;
    }

    private Function EvaluateLambda(Sem.Lambda lambda, Environment env) => new Function(
        lambda.args.Map(a => a.identifier.identifier.value).ToList(),
        lambda.body,
        new Environment(env)
    );

    private Value EvaluateProg(Sem.Prog prog, Environment env) {
        var localEnv = new Environment(env);

        foreach (var varIdent in prog.vars) {
            var name = varIdent.identifier.identifier.value;
            localEnv.Define(name, Null.Instance);
        }

        foreach (var elem in prog.body) {
            EvaluateElement(elem, localEnv);
        }

        return EvaluateExpr(prog.last, localEnv);
    }

    private Value EvaluateCond(Sem.Cond cond, Environment env) {
        if (EvaluateExpr(cond.cond, env) is not Bool(var condition)) {
            throw new Exception("cond expects a bool as the first argument");
        }

        if (condition) {
            return EvaluateExpr(cond.t, env);
        } else if (cond.f is not null) {
            return EvaluateExpr(cond.f, env);
        } else {
            return Null.Instance;
        }
    }

    private Null EvaluateWhile(Sem.While @while, Environment env) {
        while (EvaluateExpr(@while.cond, env) is Bool { Value: true }) {
            try {
                EvaluateExpr(@while.body, env);
            } catch (BreakException) {
                break;
            }
        }

        return Null.Instance;
    }

    private Value EvaluateReturn(Sem.Return @return, Environment env) =>
        throw new ReturnException(EvaluateExpr(@return.value, env));

    private Value EvaluateFunApp(Sem.FunApp funApp, Environment env) {
        var function = EvaluateExpr(funApp.fun, env);
        var args = funApp.args.Select(arg => EvaluateExpr(arg, env)).ToList();

        return function switch {
            BuiltinFunction builtin => builtin.Implementation(args),
            Function userFunc => CallUserFunction(userFunc, args),
            _ => throw new Exception($"Attempt to call non-function at {funApp.TryGetSpan()?.PrettyPrint()}")
        };
    }

    private Value CallUserFunction(Function function, List<Value> args) {
        if (args.Count != function.Parameters.Count) {
            throw new Exception($"Function expects {function.Parameters.Count} arguments, got {args.Count}");
        }

        var localEnv = new Environment(function.Closure);

        foreach (var (param, arg) in function.Parameters.Zip(args)) {
            localEnv.Define(param, arg);
        }

        try {
            return EvaluateExpr(function.Body, localEnv);
        } catch (ReturnException ex) {
            return ex.Value;
        }
    }

    private Value EvaluateQuote(Sem.Quote quote) =>
        ConvertElementToValue(quote.element);

    private Value EvaluateIdentifier(Sem.Identifier ident, Environment env) =>
        env.Get(ident.identifier.identifier.value, ident.identifier.identifier.span)
            ?? throw new Exception($"Unknown identifier: {ident.identifier.identifier.value} at {ident.identifier.identifier.span}");

    private Value ConvertElementToValue(Element element) => element switch {
        Element.List list => new List(list.elements.Select(ConvertElementToValue).ToList()),
        Element.Identifier ident => new Atom(ident.identifier.value),
        Element.Integer integer => new Integer(integer.integer.value),
        Element.Real real => new Real(real.real.value),
        Element.Bool @bool => new Bool(@bool.boolean.value),
        Element.Null => Null.Instance,
        Element.SpecialForm specialForm => new Atom(specialForm.specialForm.type.ToString()),
        Element.Quote quote => ConvertElementToValue(quote.quote),
        _ => throw new Exception($"Cannot convert element to value: {element.GetType().Name}")
    };

    private Value EvaluateListAsCode(List list) {
        try {
            var elements = ConvertValueToListToElements(list);

            var tempAst = new Ast(elements);

            var semAst = Analyzer.Analyze(tempAst);
            return semAst.Match(
                Left: error => throw new Exception($"Semantic error in eval: {error.message}"),
                Right: semAstRight => {
                    var evalEnv = new Environment(_globalEnv);
                    Value result = Null.Instance;
                    foreach (var elem in semAstRight.elements) {
                        result = EvaluateElement(elem, evalEnv);
                    }
                    return result;
                }
            );
        }
        catch (Exception ex) {
            throw new Exception($"Error in eval: {ex.Message}");
        }
    }

    private List<Element> ConvertValueToListToElements(List list) =>
        list.Values.Map(ConvertValueToElement).ToList();

    private Element ConvertValueToElement(Value value) => value switch {
        Atom atom => new Element.Identifier(new Token.Identifier(atom.Name, new Span())),
        Integer integer => new Element.Integer(new Token.Integer(integer.Value, new Span())),
        Real real => new Element.Real(new Token.Real(real.Value, new Span())),
        Bool @bool => new Element.Bool(new Token.Bool(@bool.Value, new Span())),
        Null => new Element.Null(new Token.Null(new Span())),
        List list => new Element.List(ConvertValueToListToElements(list)),
        _ => throw new Exception($"Cannot convert value to element: {value.GetType().Name}")
    };

    private void InitializeBuiltins() {
        AddBuiltinFunctionToGlobalEnv("plus", args =>
            args is not [var x, var y]
                ? throw new Exception($"plus expects 2 arguments, got {args.Count}")
                : (x, y) switch {
                    (Integer a, Integer b) => new Integer(a.Value + b.Value),
                    (Real a, Real b) => new Real(a.Value + b.Value),
                    (Integer a, Real b) => new Real(a.Value + b.Value),
                    (Real a, Integer b) => new Real(a.Value + b.Value),
                    _ => throw new Exception("plus requires numeric arguments")
                }
        );

        AddBuiltinFunctionToGlobalEnv("minus", args =>
            args is not [var x, var y]
                ? throw new Exception($"minus expects 2 arguments, got {args.Count}")
                : (x, y) switch {
                    (Integer a, Integer b) => new Integer(a.Value - b.Value),
                    (Real a, Real b) => new Real(a.Value - b.Value),
                    (Integer a, Real b) => new Real(a.Value - b.Value),
                    (Real a, Integer b) => new Real(a.Value - b.Value),
                    _ => throw new Exception("minus requires numeric arguments")
                }
        );

        AddBuiltinFunctionToGlobalEnv("times", args =>
            args is not [var x, var y]
                ? throw new Exception($"times expects 2 arguments, got {args.Count}")
                : (x, y) switch {
                    (Integer a, Integer b) => new Integer(a.Value * b.Value),
                    (Real a, Real b) => new Real(a.Value * b.Value),
                    (Integer a, Real b) => new Real(a.Value * b.Value),
                    (Real a, Integer b) => new Real(a.Value * b.Value),
                    _ => throw new Exception("times requires numeric arguments")
                }
        );

        AddBuiltinFunctionToGlobalEnv("divide", args =>
            args is not [var x, var y]
                ? throw new Exception($"divide expects 2 arguments, got {args.Count}")
                : (x, y) switch {
                    (Integer a, Integer b) when b.Value != 0 => new Integer(a.Value / b.Value),
                    (Real a, Real b) when b.Value != 0 => new Real(a.Value / b.Value),
                    (Integer a, Real b) when b.Value != 0 => new Real(a.Value / b.Value),
                    (Real a, Integer b) when b.Value != 0 => new Real(a.Value / b.Value),
                    _ => throw new Exception("divide requires numeric arguments and non-zero divisor")
                }
        );

        AddBuiltinFunctionToGlobalEnv("abs", args =>
            args is not [var x]
                ? throw new Exception($"abs expects 1 arguments, got {args.Count}")
                : x switch {
                    Integer a => new Integer(Math.Abs(a.Value)),
                    Real a => new Real(Math.Abs(a.Value)),
                    _ => throw new Exception("abs requires one numeric argument")
                }
        );

        AddBuiltinFunctionToGlobalEnv("modulo", args =>
            args is not [var x, var y]
                ? throw new Exception($"divide expects 2 arguments, got {args.Count}")
                : (x, y) switch {
                    (Integer a, Integer b) when b.Value != 0 => new Integer(a.Value % b.Value),
                    _ => throw new Exception("modulo requires integer arguments and non-zero divisor")
                }
        );

        AddBuiltinFunctionToGlobalEnv("sqrt", args =>
            args is not [var x]
                ? throw new Exception($"sqrt expects 1 arguments, got {args.Count}")
                : x switch {
                    Integer a => new Real(Math.Sqrt(a.Value)),
                    Real a    => new Real(Math.Sqrt(a.Value)),
                    _ => throw new Exception("sqrt requires one numeric argument")
                }
        );

        AddBuiltinFunctionToGlobalEnv("head", args =>
            args is not [var x]
                ? throw new Exception($"head expects 1 arguments, got {args.Count}")
                : x switch {
                    List { Values: [var first, ..] } => first,
                    List { Values: [] } => throw new Exception("head called on empty list"),
                    _ => throw new Exception("head requires a list argument")
                }
        );

        AddBuiltinFunctionToGlobalEnv("tail", args =>
            args is not [var x]
                ? throw new Exception($"tail expects 1 arguments, got {args.Count}")
                : x switch {
                    List { Values: [_, ..var rest] } => new List(rest),
                    List { Values: [] } => throw new Exception("tail called on empty list"),
                    _ => throw new Exception("tail requires a list argument")
                }
        );

        AddBuiltinFunctionToGlobalEnv("cons", args =>
            args is not [var head, var tail]
                ? throw new Exception($"cons expects 2 argumenta, got {args.Count}")
                : tail switch {
                    List list => new List([head, ..list.Values]),
                    _ => throw new Exception("cons requires a list as second argument")
                }
        );

        AddBuiltinFunctionToGlobalEnv("equal", args =>
            args is not [var a, var b]
                ? throw new Exception($"equal expects 2 arguments, got {args.Count}")
                : new Bool(ValuesEqual(a, b))
        );

        AddBuiltinFunctionToGlobalEnv("nonequal", args =>
            args is not [var a, var b]
                ? throw new Exception($"nonequal expects 2 arguments, got {args.Count}")
                : new Bool(!ValuesEqual(a, b))
        );

        AddBuiltinFunctionToGlobalEnv("less", args =>
            args is not [var a, var b]
                ? throw new Exception($"less expects 2 arguments, got {args.Count}")
                : new Bool(CompareValues(a, b) < 0)
        );

        AddBuiltinFunctionToGlobalEnv("lesseq", args =>
            args is not [var a, var b]
                ? throw new Exception($"lesseq expects 2 arguments, got {args.Count}")
                : new Bool(CompareValues(a, b) <= 0)
        );

        AddBuiltinFunctionToGlobalEnv("greater", args =>
            args is not [var a, var b]
                ? throw new Exception($"greater expects 2 arguments, got {args.Count}")
                : new Bool(CompareValues(a, b) > 0)
        );

        AddBuiltinFunctionToGlobalEnv("greatereq", args =>
            args is not [var a, var b]
                ? throw new Exception($"greatereq expects 2 arguments, got {args.Count}")
                : new Bool(CompareValues(a, b) >= 0)
        );

        AddBuiltinFunctionToGlobalEnv("isint", args =>
            args is not [var x]
                ? throw new Exception($"isint expects 1 argument, got {args.Count}")
                : new Bool(x is Integer)
        );

        AddBuiltinFunctionToGlobalEnv("isreal", args =>
            args is not [var x]
                ? throw new Exception($"isreal expects 1 argument, got {args.Count}")
                : new Bool(x is Real)
        );

        AddBuiltinFunctionToGlobalEnv("isbool", args =>
            args is not [var x]
                ? throw new Exception($"isbool expects 1 argument, got {args.Count}")
                : new Bool(x is Bool)
        );

        AddBuiltinFunctionToGlobalEnv("isnull", args =>
            args is not [var x]
                ? throw new Exception($"isnull expects 1 argument, got {args.Count}")
                : new Bool(x is Null)
        );

        AddBuiltinFunctionToGlobalEnv("isatom", args =>
            args is not [var x]
                ? throw new Exception($"isatom expects 1 argument, got {args.Count}")
                : new Bool(x is Atom)
        );

        AddBuiltinFunctionToGlobalEnv("islist", args =>
            args is not [var x]
                ? throw new Exception($"islist expects 1 argument, got {args.Count}")
                : new Bool(x is List)
        );

        AddBuiltinFunctionToGlobalEnv("and", args =>
            args is not [var a, var b]
                ? throw new Exception($"and expects 2 arguments, got {args.Count}")
                : new Bool(GetBoolValue(a) && GetBoolValue(b))
        );

        AddBuiltinFunctionToGlobalEnv("or", args =>
            args is not [var a, var b]
                ? throw new Exception($"or expects 2 arguments, got {args.Count}")
                : new Bool(GetBoolValue(a) || GetBoolValue(b))
        );

        AddBuiltinFunctionToGlobalEnv("xor", args =>
            args is not [var a, var b]
                ? throw new Exception($"xor expects 2 arguments, got {args.Count}")
                : new Bool(GetBoolValue(a) != GetBoolValue(b))
        );

        AddBuiltinFunctionToGlobalEnv("not", args =>
            args is not [var a]
                ? throw new Exception($"not expects 1 argument, got {args.Count}")
                : new Bool(!GetBoolValue(a))
        );

        AddBuiltinFunctionToGlobalEnv("eval", args =>
            args is not [var x]
                ? throw new Exception($"eval expects 1 argument, got {args.Count}")
                : x switch {
                    List list => EvaluateListAsCode(list),
                    Atom atom => _globalEnv.Get(atom.Name)
                        ?? throw new Exception($"Unknown identifier: {atom.Name}"),
                    _ => x
                }
        );
    }

    private void AddBuiltinFunctionToGlobalEnv(string name, Func<List<Value>, Value> func) =>
        _globalEnv.Define(name, new BuiltinFunction(name, func));

    private static void ValidateArgsCounta(string functionName, int expected, List<Value> args) {
        if (args.Count != expected) {
            throw new Exception($"{functionName} expects {expected} arguments, got {args.Count}");
        }
    }

    private static bool ValuesEqual(Value a, Value b) => (a, b) switch {
        (Integer i1, Integer i2) => i1.Value == i2.Value,
        (Real r1, Real r2) => Math.Abs(r1.Value - r2.Value) < double.Epsilon,
        (Integer i, Real r) => Math.Abs(i.Value - r.Value) < double.Epsilon,
        (Real r, Integer i) => Math.Abs(r.Value - i.Value) < double.Epsilon,
        (Bool b1, Bool b2) => b1.Value == b2.Value,
        (Null, Null) => true,
        (Atom a1, Atom a2) => a1.Name == a2.Name,
        (List l1, List l2) => l1.Values.SequenceEqual(l2.Values, new ValueEqualityComparer()),
        _ => false
    };

    private static int CompareValues(Value a, Value b) => (a, b) switch {
        (Integer i1, Integer i2) => i1.Value.CompareTo(i2.Value),
        (Real r1, Real r2) => r1.Value.CompareTo(r2.Value),
        (Integer i, Real r) => i.Value.CompareTo(r.Value),
        (Real r, Integer i) => r.Value.CompareTo(i.Value),
        _ => throw new Exception("Comparison requires numeric arguments")
    };

    private static bool GetBoolValue(Value value) => value switch {
        Bool b => b.Value,
        _ => throw new Exception("Expected boolean value")
    };

    private class ValueEqualityComparer : IEqualityComparer<Value> {
        public bool Equals(Value? x, Value? y) => ValuesEqual(x!, y!);
        public int GetHashCode(Value obj) => obj.GetHashCode();
    }
}
