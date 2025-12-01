using static FCompiler.Lexer.Token.SpecialForm.Type;
using FCompiler.Parser;
using FCompiler.Utils;
using LanguageExt;

namespace FCompiler.Semantic;

public class Analyzer {
    public record Error(string message, Span? span = null) {
        public static implicit operator Error(string message) => new Error(message);
    }

    public static Either<Error, Sem.Ast> Analyze(Ast ast) =>
        new Analyzer().Run(ast);

    private Scope _currentScope;

    private Analyzer() {
        _currentScope = new Scope("global", Scope.Type.Global);
        AddBuiltinFunctions();
    }

    private Either<Error, Sem.Ast> Run(Ast ast) =>
        ast.elements
            .Sequence(AnalyzeElement)
            .Map(elems => new Sem.Ast(elems.ToList()));

    private void AddBuiltinFunctions() {
        var builtins = new[] {
            "plus", "minus", "times", "divide",
            "head", "tail", "cons",
            "equal", "nonequal", "less", "lesseq", "greater", "greatereq",
            "isint", "isreal", "isbool", "isnull", "isatom", "islist",
            "and", "or", "xor", "not", "eval",
        };

        foreach (var builtin in builtins)
            _currentScope.TryAddSymbol(builtin);
    }

    private Either<Error, Sem.Elem> AnalyzeElement(Element element) => element switch {
        Element.List list => AnalyzeList(list),
        Element.Quote quote      => new Sem.Quote(quote),
        Element.Identifier ident => new Sem.Identifier(ident),
        Element.Integer integer  => new Sem.Integer(integer),
        Element.Real real        => new Sem.Real(real),
        Element.Null @null       => new Sem.Null(@null),
        Element.Bool @bool       => new Sem.Bool(@bool),
        _ => new Error($"unexpected {element.GetType().ToString()}")
    };

    private Either<Error, Sem.Expr> AnalyzeExpr(Element element) => element switch {
        Element.List list => AnalyzeList(list).Bind<Sem.Expr>(l =>
            l is Sem.Expr expr
                ? expr
                : new Error("expected proper expression in this context")
        ),
        Element.Quote quote      => new Sem.Quote(quote),
        Element.Identifier ident => new Sem.Identifier(ident),
        Element.Null @null       => new Sem.Null(@null),
        Element.Integer integer  => new Sem.Integer(integer),
        Element.Real real        => new Sem.Real(real),
        Element.Bool @bool       => new Sem.Bool(@bool),
        Element.SpecialForm specialForm => new Error($"unexpected special form {specialForm}"),
        _ => throw new ArgumentException($"Unknown element type")
    };

    private Either<Error, Sem.Elem> AnalyzeList(Element.List list) => list.elements switch {
        [] => new Error("() is invalid"),
        [Element.SpecialForm specialForm, ..var tail] => AnalyzeSpecialForm(specialForm, tail),
        [var head, ..var tail] => AnalyzeFunApp(head, tail).Map(f => (Sem.Elem)f),
    };


    private Either<Error, Sem.Elem> AnalyzeSpecialForm(
        Element.SpecialForm specialForm,
        List<Element> args
    ) => specialForm.specialForm.type switch {
        Setq   => AnalyzeSetq(args).Map(s => (Sem.Elem)s),
        Func   => AnalyzeFunc(args).Map(f => (Sem.Elem)f),
        Lambda => AnalyzeLambda(args).Map(l => (Sem.Elem)l),
        Prog   => AnalyzeProg(args).Map(p => (Sem.Elem)p),
        Cond   => AnalyzeCond(args).Map(c => (Sem.Elem)c),
        While  => AnalyzeWhile(args).Map(w => (Sem.Elem)w),
        Return => AnalyzeReturn(args).Map(r => (Sem.Elem)r),
        Break  => AnalyzeBreak(args).Map(b => (Sem.Elem)b),
        Quote  => new Sem.Quote(new Element.List(args)),
        _ => throw new ArgumentException($"Unknown specialForm type")
    };

    private Either<Error, Sem.FunApp> AnalyzeFunApp(Element funElem, List<Element> argElems) =>
        from fun in AnalyzeExpr(funElem)
        from args in argElems.Sequence(AnalyzeExpr)
        select new Sem.FunApp(fun, args.ToList());

    private Either<Error, Sem.Setq> AnalyzeSetq(List<Element> args) {
        if (args is not [Element.Identifier ident, var bodyElem]) {
            return new Error("setq requires exactly 2 arguments and first argument has to be identifier");
        }

        _currentScope.TryAddSymbol(ident.identifier.value);

        return
            from body in AnalyzeExpr(bodyElem)
            select new Sem.Setq(new(ident), body);
    }

    private Either<Error, Sem.Fun> AnalyzeFunc(List<Element> args) {
        if (args is not [Element.Identifier ident, Element.List paramList, var bodyElem]) {
            return new Error("func requires exactly 3 arguments");
        }

        var parameters = new List<Sem.Identifier>();
        foreach (var param in paramList.elements) {
            if (param is Element.Identifier paramIdent)
                parameters.Add(new Sem.Identifier(paramIdent));
            else
                return new Error("Function parameter must be an identifier");
        }

        var name = ident.identifier.value;

        if (!_currentScope.TryAddSymbol(name))
            return new Error($"Function '{name}' is already declared", ident.identifier.span);

        var functionScope = new Scope(name, Scope.Type.Function, _currentScope);

        foreach (var param in parameters)
            functionScope.TryAddSymbol(param.identifier.identifier.value);

        var oldScope = _currentScope;
        _currentScope = functionScope;

        var fun =
            from body in AnalyzeExpr(bodyElem)
            select new Sem.Fun(new(ident), parameters, body);

        _currentScope = oldScope;

        return fun;
    }

    private Either<Error, Sem.Lambda> AnalyzeLambda(List<Element> args) {
        if (args is not [Element.List paramList, var bodyElem]) {
            return new Error("lambda requires exactly 2 arguments and first argument has to be a list");
        }

        var lambdaScope = new Scope("lambda", Scope.Type.Function, _currentScope);
        var identifierList = new List<Sem.Identifier>();

        foreach (var param in paramList.elements) {
            if (param is Element.Identifier paramIdent) {
                lambdaScope.TryAddSymbol(paramIdent.identifier.value);
                identifierList.Add(new Sem.Identifier(paramIdent));
            } else
                return new Error("Lambda argument must be an identifier");
        }

        var oldScope = _currentScope;
        _currentScope = lambdaScope;

        var lambda =
            from body in AnalyzeExpr(bodyElem)
            select new Sem.Lambda(identifierList, body);

        _currentScope = oldScope;

        return lambda;
    }

    private Either<Error, Sem.Prog> AnalyzeProg(List<Element> args) {
        if (args is not [Element.List varList, ..var bodyElem, var lastElem]) {
            return new Error("prog has to have at least 2 argument and first argument has to be a list");
        }

        var progScope = new Scope("prog", Scope.Type.Prog, _currentScope);
        var identifierList = new List<Sem.Identifier>();

        foreach (var variable in varList.elements) {
            if (variable is Element.Identifier varIdent) {
                progScope.TryAddSymbol(varIdent.identifier.value);
                identifierList.Add(new Sem.Identifier(varIdent));
            } else
                return new Error("Prog variable declaration must be an identifier");
        }

        var oldScope = _currentScope;
        _currentScope = progScope;

        var prog =
            from body in bodyElem.Sequence(AnalyzeElement)
            from last in AnalyzeExpr(lastElem)
            select new Sem.Prog(identifierList, body.ToList(), last);

        _currentScope = oldScope;

        return prog;
    }

    private Either<Error, Sem.While> AnalyzeWhile(List<Element> args) {
        if (args is not [var condElem, var bodyElem]) {
            return new Error("while requires exactly 2 arguments");
        }

        var loopScope = new Scope("while", Scope.Type.While, _currentScope);

        var oldScope = _currentScope;
        _currentScope = loopScope;

        var @while =
            from cond in AnalyzeExpr(condElem)
            from body in AnalyzeExpr(bodyElem)
            select new Sem.While(cond, body);

        _currentScope = oldScope;

        return @while;
    }

    private Either<Error, Sem.Return> AnalyzeReturn(List<Element> args) =>
          args is not [var expr]                   ? new Error("return has to have exactly one argument")
        : !_currentScope.IsIn(Scope.Type.Function) ? new Error("return can only be used inside a function")
        : AnalyzeExpr(expr).Bind<Sem.Return>(e => new Sem.Return(e));

    private Either<Error, Sem.Break> AnalyzeBreak(List<Element> args) =>
          args is not []                        ? new Error("break has to have exactly zero arguments")
        : !_currentScope.IsIn(Scope.Type.While) ? new Error("break can only be used inside a loop")
        : Sem.Break.Default;

    private Either<Error, Sem.Cond> AnalyzeCond(List<Element> args) => args switch {
        [var condElem, var tElem] =>
            from cond in AnalyzeExpr(condElem)
            from t    in AnalyzeExpr(tElem)
            select new Sem.Cond(cond, t),
        [var condElem, var tElem, var fElem] =>
            from cond in AnalyzeExpr(condElem)
            from t    in AnalyzeExpr(tElem)
            from f    in AnalyzeExpr(fElem)
            select new Sem.Cond(cond, t, f),
        _ => new Error("cond has to have two or tree arguments")
    };

    private Either<Error, Element.Identifier> AnalyzeIdentifier(Element.Identifier ident) {
        var name = ident.identifier.value;
        if (_currentScope.Contains(name) is false)
            return new Error($"Identifier '{name}' is not declared in current scope", ident.identifier.span);
        else
            return ident;
    }
}
