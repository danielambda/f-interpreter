using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using LanguageExt;
using System.Collections.Immutable;

namespace FCompiler.Optimizations;

public record FunInfo(List<Sem.Identifier> args, Sem.Expr body);

public class Optimizer {
    public static Sem.Ast Optimize(Sem.Ast ast) =>
        new Optimizer(ast).Run();

    private readonly Sem.Ast _ast;
    private readonly Dictionary<string, FunInfo> _functions;
    private readonly Dictionary<string, int> _variableUsageCount;

    private Optimizer(Sem.Ast ast) {
        _ast = ast;
        _functions = [];
        _variableUsageCount = [];
    }

    private Sem.Ast Run() {
        CollectFunctionDefinitions(_ast.elements);
        CollectVariableUsage(_ast.elements);

        var optimizedElements = _ast.elements.Select(OptimizeElement).ToList();

        optimizedElements = RemoveUnusedDecls(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);

        _functions.Clear();
        _variableUsageCount.Clear();
        CollectFunctionDefinitions(optimizedElements);
        CollectVariableUsage(optimizedElements);

        optimizedElements = RemoveUnusedDecls(optimizedElements);
        optimizedElements = optimizedElements.Select(OptimizeElement).ToList();

        return new Sem.Ast(optimizedElements);
    }

    private void CollectFunctionDefinitions(List<Sem.Elem> elements) {
        foreach (var element in elements) {
            if (element is Sem.Fun(var funcName, var paramList, var body)) {
                _functions[funcName.identifier.identifier.value] = new FunInfo(paramList, body);
            }
        }
    }

    private void CollectVariableUsage(IEnumerable<Sem.Elem> elements) =>
        elements.ToList().ForEach(CountVariableUsage);

    private void CountVariableUsage(Sem.Elem element) {
        switch (element) {
            case Sem.Setq(_, var value):
                CountVariableUsage(value);
                break;
            case Sem.Fun(_, _, var body):
                CountVariableUsage(body);
                break;
            case Sem.Lambda(_, var body):
                CountVariableUsage(body);
                break;
            case Sem.Prog(_, var body, var last):
                CollectVariableUsage(body);
                CountVariableUsage(last);
                break;
            case Sem.Cond(var cond, var t, var f):
                CountVariableUsage(cond);
                CountVariableUsage(t);
                if (f is not null) CountVariableUsage(f);
                break;
            case Sem.While(var cond, var body):
                CountVariableUsage(cond);
                CountVariableUsage(body);
                break;
            case Sem.Return(var value):
                CountVariableUsage(value);
                break;
            case Sem.FunApp(var f, var x):
                CountVariableUsage(f);
                CollectVariableUsage(x);
                break;
            case Sem.Identifier ident:
                var varName = ident.identifier.identifier.value;
                _variableUsageCount[varName] = _variableUsageCount.GetValueOrDefault(varName) + 1;
                break;
        }
    }

    private Sem.Elem OptimizeElement(Sem.Elem element) => element switch {
        _ => element
    };

    private Sem.Expr? TryOptimizeArithmetic(Sem.FunApp funApp) => funApp switch {
        Sem.FunApp(Sem.Identifier op, [Sem.Integer i1, Sem.Integer i2])
            when OptimizeArithmetic(
                op.identifier.identifier.value,
                i1.integer.integer.value,
                i2.integer.integer.value
            ) is {} result => new Sem.Integer(new Element.Integer(new Token.Integer(result, i1.integer.integer.span))),
        Sem.FunApp(Sem.Identifier op, [Sem.Real r1, Sem.Real r2])
            when OptimizeArithmetic(
                op.identifier.identifier.value,
                r1.real.real.value,
                r2.real.real.value
            ) is {} result => new Sem.Real(new Element.Real(new Token.Real(result, r1.real.real.span))),
        _ => null!
    };

    private double? OptimizeArithmetic(string operation, double a, double b) => operation switch {
        "plus" => a + b,
        "minus" => a - b,
        "times" => a * b,
        "divide" when b != 0 => a / b,
        _ => null
    };

    private long? OptimizeArithmetic(string operation, long a, long b) => operation switch {
        "plus" => a + b,
        "minus" => a - b,
        "times" => a * b,
        "divide" when b != 0 => a / b,
        _ => null
    };

    private List<Sem.Elem> RemoveUnusedDecls(List<Sem.Elem> elements) {
        var result = new List<Sem.Elem>();

        foreach (var element in elements) {
            if (element is Sem.Setq(var qName, _)) {
                if (_variableUsageCount.GetValueOrDefault(qName.identifier.identifier.value) > 0)
                    result.Add(element);
            } else if (element is Sem.Fun(var funName, _, _)) {
                if (_variableUsageCount.GetValueOrDefault(funName.identifier.identifier.value) > 0)
                    result.Add(element);
            } else if (element is Sem.Prog prog) {
                result.Add(prog with { body = RemoveUnusedDecls(prog.body) });
            } else {
                result.Add(element);
            }
        }

        return result;
    }

    private List<Sem.Elem> InlineFunctions(List<Sem.Elem> elements) {
        return elements.Map(Inline).ToList();

        Sem.Elem Inline(Sem.Elem elem) => elem switch {
            Sem.Setq setq => setq with { body = InlineExpr(setq.body) },
            Sem.Expr expr => InlineExpr(expr),
            _ => elem,
        };

        Sem.Expr InlineExpr(Sem.Expr expr) => expr switch {
            Sem.Prog prog => prog with { body = InlineFunctions(prog.body), last = InlineExpr(prog.last) },
            Sem.FunApp funApp when TryInlineFunctionCall(funApp) is { } inlined => inlined,
            _ => expr,
        };
    }

    private Sem.Expr? TryInlineFunctionCall(Sem.FunApp funApp) => funApp switch {
        Sem.FunApp(Sem.Identifier ident, var args)
            when _functions.TryGetValue(ident.identifier.identifier.value, out var func)
              && args.Count == func.args.Count
            => BetaReduce(args, func.args, func.body),
        Sem.FunApp(Sem.Lambda lambda, var args)
            when args.Count == lambda.args.Count
            => BetaReduce(args, lambda.args, lambda.body),
        _ => null,
    };

    private Sem.Expr BetaReduce(
        List<Sem.Expr> passedArgs,
        List<Sem.Identifier> expectedArgs,
        Sem.Expr body
    ) {
        // TODO handle names collision
        var paramMap = expectedArgs.Zip(passedArgs).ToDictionary();
        return ReplaceParameters(body, paramMap);
    }

    private Sem.Expr ReplaceParameters(
        Sem.Expr body,
        Dictionary<Sem.Identifier, Sem.Expr> paramMap
    ) => body switch {
        Sem.Identifier ident when paramMap.TryGetValue(ident, out var param) => param,
        Sem.FunApp(var fun, var args) => new Sem.FunApp(
            ReplaceParameters(fun, paramMap),
            args.Map(a => ReplaceParameters(a, paramMap)).ToList()
        ),
        Sem.Cond(var cond, var t, var f) => new Sem.Cond(
            ReplaceParameters(cond, paramMap),
            ReplaceParameters(t   , paramMap),
            f is {} ff ? ReplaceParameters(ff, paramMap) : null
        ),
        Sem.While(var cond, var whileBody) => new Sem.While(
            ReplaceParameters(cond,      paramMap),
            ReplaceParameters(whileBody, paramMap)
        ),
        Sem.Return(var value) => new Sem.Return( ReplaceParameters(value, paramMap)),
        _ => body
    };
}
