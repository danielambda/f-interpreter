using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using LanguageExt;
using System.Collections.Immutable;

namespace FCompiler.Optimizations;

public record FunInfo(ImmutableList<Sem.Identifier> args, Sem.Expr body);

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

        var optimizedElements = _ast.elements;

        optimizedElements = RemoveUnusedDecls(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);

        _functions.Clear();
        _variableUsageCount.Clear();
        CollectFunctionDefinitions(optimizedElements);
        CollectVariableUsage(optimizedElements);

        optimizedElements = RemoveUnusedDecls(optimizedElements);

        optimizedElements = TryOptimizeArithmetic(optimizedElements);

        return new Sem.Ast(optimizedElements);
    }

    private void CollectFunctionDefinitions(ImmutableList<Sem.Elem> elements) {
        foreach (var element in elements) {
            if (element is Sem.Fun(var funcName, var paramList, var body)) {
                _functions[funcName.identifier.identifier.value] = new FunInfo(paramList, body);
            } else if (element is Sem.Setq(var funcNameL, Sem.Lambda(var paramListL, var bodyL))) {
                _functions[funcNameL.identifier.identifier.value] = new FunInfo(paramListL, bodyL);
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

    private ImmutableList<Sem.Elem> TryOptimizeArithmetic(ImmutableList<Sem.Elem> ast) =>
        ast.Map(elem =>
            elem switch {
                Sem.FunApp funApp => TryOptimizeArithmetic(funApp) ?? funApp,
                var other => other
            }
        ).ToImmutableList();

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

    private ImmutableList<Sem.Elem> RemoveUnusedDecls(ImmutableList<Sem.Elem> elements) {
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

        return result.ToImmutableList();
    }

    private ImmutableList<Sem.Elem> InlineFunctions(ImmutableList<Sem.Elem> elements) {
        return elements.Map(InlineElem).ToImmutableList();

        Sem.Elem InlineElem(Sem.Elem elem) => elem switch {
            Sem.Fun  func => func with { body = InlineExpr(func.body) },
            Sem.Expr expr => InlineExpr(expr),
            _ => elem,
        };

        Sem.Expr InlineExpr(Sem.Expr expr) => expr switch {
            Sem.Setq setq => setq with { body = InlineExpr(setq.body) },
            Sem.Lambda lambda => lambda with { body = InlineExpr(lambda.body) },
            Sem.Prog prog => prog with {
                body = prog.body.Map(InlineElem).ToImmutableList(),
                last = InlineExpr(prog.last)
            },
            Sem.Cond(var cond, var t, var f) => new Sem.Cond(
                InlineExpr(cond),
                InlineExpr(t),
                f is {} ff ? InlineExpr(ff) : null
            ),
            Sem.While(var cond, var body) => new Sem.While(
                InlineExpr(cond),
                InlineExpr(body)
            ),
            Sem.FunApp funApp when TryInlineFunApp(funApp) is {} inlined => inlined,
            Sem.FunApp(var fun, var args) => new Sem.FunApp(
                InlineExpr(fun),
                args.Map(InlineExpr).ToImmutableList()
            ),
            var other => other,
        };
    }

    private Sem.Expr? TryInlineFunApp(Sem.FunApp funApp) => funApp switch {
        Sem.FunApp(Sem.Identifier ident, var args)
            when _functions.TryGetValue(ident.identifier.identifier.value, out var func)
              && args.Count == func.args.Count
            => BetaReduce(args, func.args, func.body),
        Sem.FunApp(Sem.Lambda lambda, var args)
            when args.Count == lambda.args.Count
            => BetaReduce(args, lambda.args, lambda.body),
        _ => null,
    };

    private Sem.Expr? BetaReduce(
        ImmutableList<Sem.Expr> passedArgs,
        ImmutableList<Sem.Identifier> expectedArgs,
        Sem.Expr body
    ) => CanInline(passedArgs, expectedArgs, body)
        ? ReplaceParameters(
            body,
            paramMap: expectedArgs.Map(a => a.identifier.identifier.value).Zip(passedArgs).ToDictionary()
        )
        : null;

    private bool CanInline(
        ImmutableList<Sem.Expr> passedArgs,
        ImmutableList<Sem.Identifier> expectedArgs,
        Sem.Expr body
    ) {
        var collisingIdentifiers = IdentifiersIn(body)
            .Except(expectedArgs.Map(a => a.identifier.identifier.value))
            .ToHashSet();
        return !passedArgs.Any(arg => IdentifiersIn(arg).Any(collisingIdentifiers.Contains));
    }

    private ImmutableList<string> IdentifiersIn(Sem.Expr expr) => expr switch {
        Sem.Setq(var name, var body) => IdentifiersIn(name).AddRange(IdentifiersIn(body)),
        Sem.Lambda(var args, var body) => args.Bind(IdentifiersIn).ToImmutableList().AddRange(IdentifiersIn(body)),
        Sem.Prog(var vars, var _body, var last) => vars.Bind(IdentifiersIn).ToImmutableList().AddRange(IdentifiersIn(last)),
        Sem.Cond(var cond, var t, var f) => IdentifiersIn(cond).AddRange(IdentifiersIn(t)).AddRange(f is {} ff ? IdentifiersIn(ff) : []),
        Sem.While(var cond, var body) => IdentifiersIn(cond).AddRange(IdentifiersIn(body)),
        Sem.Return(var value) => IdentifiersIn(value),
        Sem.FunApp(var fun, var args) => IdentifiersIn(fun).AddRange(args.Bind(IdentifiersIn)),
        Sem.Identifier(var idetifier) => [idetifier.identifier.value],
        _ => []
    };

    private Sem.Expr? ReplaceParameters(
        Sem.Expr body,
        Dictionary<string, Sem.Expr> paramMap
    ) {
        return Go(body);

        Sem.Expr? Go(Sem.Expr body) => body switch {
            Sem.Identifier ident when paramMap.TryGetValue(ident.identifier.identifier.value, out var param) => param,
            Sem.Setq setq => Go(setq.body) is {} sbody
                ? setq with { body = sbody }
                : null,
            Sem.Lambda lambda => Go(lambda.body) is {} sbody
                ? lambda with { body = sbody }
                : null,
            Sem.Prog prog => null,
            Sem.Cond(var cond, var t, var f) => Go(cond) is {} scode && Go(t) is {} st
              ? f is {} ff
                ? Go(ff) is {} goff
                    ? new Sem.Cond(scode, st, goff)
                    : null
                : new Sem.Cond(scode, st)
              : null,
            Sem.While(var cond, var whileBody) => Go(cond) is {} scode && Go(whileBody) is {} sbody
                ? new Sem.While(scode, sbody)
                : null,
            Sem.Return(var value) => Go(value) is {} svalue
                ? new Sem.Return(svalue)
                : null,
            Sem.FunApp(var fun, var args) => Go(fun) is {} sfun
                                          && args.Map(Go).ToImmutableList() is var sargs
                                          && sargs.All(a => a is not null)
                ? new Sem.FunApp(sfun, sargs!)
                : null,
            _ => body
        };
    }
}
