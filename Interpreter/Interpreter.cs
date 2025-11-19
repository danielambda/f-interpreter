using FCompiler.Parser;
using FCompiler.Semantic;
using static FCompiler.Interpreter.Value;

namespace FCompiler.Interpreter;

public class Interpreter {
    private Environment _globalEnv;

    public Interpreter() {
        _globalEnv = new Environment();
        InitializeBuiltins();
    }

    public Value Interpret(Sem.Ast ast) {
        Value result = Null.Instance;

        foreach (var elem in ast.elements) {
            result = EvaluateElement(elem, _globalEnv);
        }

        return result;
    }

    private Value EvaluateElement(Sem.Elem elem, Environment env) {
        return elem switch {
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
    }

    private Value EvaluateExpr(Sem.Expr expr, Environment env) =>
        EvaluateElement(expr, env);

    private Value EvaluateSetq(Sem.Setq setq, Environment env) {
        var value = EvaluateExpr(setq.body, env);
        var name = setq.name.identifier.identifier.value;

        if (env.Contains(name)) {
            env.Set(name, value);
        } else {
            env.Define(name, value);
        }

        return value;
    }

    private Value EvaluateFun(Sem.Fun fun, Environment env) {
        var name = fun.name.identifier.identifier.value;
        var paramNames = fun.args.Select(a => a.identifier.identifier.value).ToList();

        var function = new Function(paramNames, fun.body, new Environment(env));
        env.Define(name, function);

        return function;
    }

    private Value EvaluateLambda(Sem.Lambda lambda, Environment env) {
        var paramNames = lambda.args.Select(a => a.identifier.identifier.value).ToList();
        return new Function(paramNames, lambda.body, new Environment(env));
    }

    private Value EvaluateProg(Sem.Prog prog, Environment env) {
        var localEnv = new Environment(env);

        // Initialize local variables with null
        foreach (var varIdent in prog.vars) {
            var name = varIdent.identifier.identifier.value;
            localEnv.Define(name, Null.Instance);
        }

        // Execute body elements
        foreach (var elem in prog.body) {
            EvaluateElement(elem, localEnv);
        }

        // Return the last expression
        return EvaluateExpr(prog.last, localEnv);
    }

    private Value EvaluateCond(Sem.Cond cond, Environment env) {
        var condition = EvaluateExpr(cond.cond, env);

        if (condition is Bool { Value: true }) {
            return EvaluateExpr(cond.t, env);
        } else if (cond.f != null) {
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
                return Null.Instance;
            }
        }

        return Null.Instance;
    }


    private Value EvaluateReturn(Sem.Return @return, Environment env) {
        var value = EvaluateExpr(@return.value, env);
        throw new ReturnException(value);
    }

    private Value EvaluateFunApp(Sem.FunApp funApp, Environment env) {
        var function = EvaluateExpr(funApp.fun, env);
        var args = funApp.args.Select(arg => EvaluateExpr(arg, env)).ToList();

        return function switch {
            BuiltinFunction builtin => builtin.Implementation(args),
            Function userFunc => CallUserFunction(userFunc, args),
            _ => throw new Exception("Attempt to call non-function")
        };
    }

    private Value CallUserFunction(Function function, List<Value> args) {
        if (args.Count != function.Parameters.Count) {
            throw new Exception($"Function expects {function.Parameters.Count} arguments, got {args.Count}");
        }

        var localEnv = new Environment(function.Closure);

        for (int i = 0; i < function.Parameters.Count; i++) {
            localEnv.Define(function.Parameters[i], args[i]);
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
        env.Get(ident.identifier.identifier.value);

    private Value ConvertElementToValue(Element element) => element switch {
        Element.List list => new List(list.elements.Select(ConvertElementToValue).ToList()),
        Element.Identifier ident => new List(new List<Value> {
            new List(new List<Value> { new BuiltinFunction("quote", _ => Null.Instance) }),
            ConvertElementToValue(ident)
        }),
        Element.Integer integer => new Integer(integer.integer.value),
        Element.Real real => new Real(real.real.value),
        Element.Bool @bool => new Bool(@bool.boolean.value),
        Element.Null => Null.Instance,
        Element.SpecialForm specialForm => new List([
            new BuiltinFunction("quote", _ => Null.Instance),
            new List([new BuiltinFunction(specialForm.specialForm.type.ToString(), _ => Null.Instance)])
        ]),
        _ => throw new Exception($"Cannot convert element to value: {element.GetType().Name}")
    };

    private void InitializeBuiltins() {
        // Arithmetic functions
        _globalEnv.Define("plus", new BuiltinFunction("plus", args => {
            ValidateArgsCount("plus", 2, args);
            return (args[0], args[1]) switch {
                (Integer a, Integer b) => new Integer(a.Value + b.Value),
                (Real a, Real b) => new Real(a.Value + b.Value),
                (Integer a, Real b) => new Real(a.Value + b.Value),
                (Real a, Integer b) => new Real(a.Value + b.Value),
                _ => throw new Exception("plus requires numeric arguments")
            };
        }));

        _globalEnv.Define("minus", new BuiltinFunction("minus", args => {
            ValidateArgsCount("minus", 2, args);
            return (args[0], args[1]) switch {
                (Integer a, Integer b) => new Integer(a.Value - b.Value),
                (Real a, Real b) => new Real(a.Value - b.Value),
                (Integer a, Real b) => new Real(a.Value - b.Value),
                (Real a, Integer b) => new Real(a.Value - b.Value),
                _ => throw new Exception("minus requires numeric arguments")
            };
        }));

        _globalEnv.Define("times", new BuiltinFunction("times", args => {
            ValidateArgsCount("times", 2, args);
            return (args[0], args[1]) switch {
                (Integer a, Integer b) => new Integer(a.Value * b.Value),
                (Real a, Real b) => new Real(a.Value * b.Value),
                (Integer a, Real b) => new Real(a.Value * b.Value),
                (Real a, Integer b) => new Real(a.Value * b.Value),
                _ => throw new Exception("times requires numeric arguments")
            };
        }));

        _globalEnv.Define("divide", new BuiltinFunction("divide", args => {
            ValidateArgsCount("divide", 2, args);
            return (args[0], args[1]) switch {
                (Integer a, Integer b) when b.Value != 0 => new Integer(a.Value / b.Value),
                (Real a, Real b) when b.Value != 0 => new Real(a.Value / b.Value),
                (Integer a, Real b) when b.Value != 0 => new Real(a.Value / b.Value),
                (Real a, Integer b) when b.Value != 0 => new Real(a.Value / b.Value),
                _ => throw new Exception("divide requires numeric arguments and non-zero divisor")
            };
        }));

        // List operations
        _globalEnv.Define("head", new BuiltinFunction("head", args => {
            ValidateArgsCount("head", 1, args);
            return args[0] switch {
                List { Values: [var first, ..] } => first,
                List list when list.Values.Count == 0 => throw new Exception("head called on empty list"),
                _ => throw new Exception("head requires a list argument")
            };
        }));

        _globalEnv.Define("tail", new BuiltinFunction("tail", args => {
            ValidateArgsCount("tail", 1, args);
            return args[0] switch {
                List { Values: [_, ..var rest] } => new List(rest),
                List list when list.Values.Count == 0 => throw new Exception("tail called on empty list"),
                _ => throw new Exception("tail requires a list argument")
            };
        }));

        _globalEnv.Define("cons", new BuiltinFunction("cons", args => {
            ValidateArgsCount("cons", 2, args);
            return args[1] switch {
                List list => new List(new List<Value> { args[0] }.Concat(list.Values).ToList()),
                _ => throw new Exception("cons requires a list as second argument")
            };
        }));

        // Comparisons
        _globalEnv.Define("equal", new BuiltinFunction("equal", args => {
            ValidateArgsCount("equal", 2, args);
            return new Bool(ValuesEqual(args[0], args[1]));
        }));

        _globalEnv.Define("nonequal", new BuiltinFunction("nonequal", args => {
            ValidateArgsCount("nonequal", 2, args);
            return new Bool(!ValuesEqual(args[0], args[1]));
        }));

        _globalEnv.Define("less", new BuiltinFunction("less", args => {
            ValidateArgsCount("less", 2, args);
            return CompareValues(args[0], args[1]) < 0 ? new Bool(true) : new Bool(false);
        }));

        _globalEnv.Define("lesseq", new BuiltinFunction("lesseq", args => {
            ValidateArgsCount("lesseq", 2, args);
            return CompareValues(args[0], args[1]) <= 0 ? new Bool(true) : new Bool(false);
        }));

        _globalEnv.Define("greater", new BuiltinFunction("greater", args => {
            ValidateArgsCount("greater", 2, args);
            return CompareValues(args[0], args[1]) > 0 ? new Bool(true) : new Bool(false);
        }));

        _globalEnv.Define("greatereq", new BuiltinFunction("greatereq", args => {
            ValidateArgsCount("greatereq", 2, args);
            return CompareValues(args[0], args[1]) >= 0 ? new Bool(true) : new Bool(false);
        }));

        // Predicates
        _globalEnv.Define("isint", new BuiltinFunction("isint", args => {
            ValidateArgsCount("isint", 1, args);
            return new Bool(args[0] is Integer);
        }));

        _globalEnv.Define("isreal", new BuiltinFunction("isreal", args => {
            ValidateArgsCount("isreal", 1, args);
            return new Bool(args[0] is Real);
        }));

        _globalEnv.Define("isbool", new BuiltinFunction("isbool", args => {
            ValidateArgsCount("isbool", 1, args);
            return new Bool(args[0] is Bool);
        }));

        _globalEnv.Define("isnull", new BuiltinFunction("isnull", args => {
            ValidateArgsCount("isnull", 1, args);
            return new Bool(args[0] is Null);
        }));

        _globalEnv.Define("isatom", new BuiltinFunction("isatom", args => {
            ValidateArgsCount("isatom", 1, args);
            return new Bool(args[0] is not List);
        }));

        _globalEnv.Define("islist", new BuiltinFunction("islist", args => {
            ValidateArgsCount("islist", 1, args);
            return new Bool(args[0] is List);
        }));

        // Logical operators
        _globalEnv.Define("and", new BuiltinFunction("and", args => {
            ValidateArgsCount("and", 2, args);
            var a = GetBoolValue(args[0]);
            var b = GetBoolValue(args[1]);
            return new Bool(a && b);
        }));

        _globalEnv.Define("or", new BuiltinFunction("or", args => {
            ValidateArgsCount("or", 2, args);
            var a = GetBoolValue(args[0]);
            var b = GetBoolValue(args[1]);
            return new Bool(a || b);
        }));

        _globalEnv.Define("xor", new BuiltinFunction("xor", args => {
            ValidateArgsCount("xor", 2, args);
            var a = GetBoolValue(args[0]);
            var b = GetBoolValue(args[1]);
            return new Bool(a != b);
        }));

        _globalEnv.Define("not", new BuiltinFunction("not", args => {
            ValidateArgsCount("not", 1, args);
            var a = GetBoolValue(args[0]);
            return new Bool(!a);
        }));

        // Evaluator
        _globalEnv.Define("eval", new BuiltinFunction("eval", args => {
            ValidateArgsCount("eval", 1, args);
            return args[0] switch {
                List => throw new NotImplementedException("eval for lists not implemented"),
                _ => args[0]
            };
        }));
    }

    private static void ValidateArgsCount(string functionName, int expected, List<Value> args) {
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
