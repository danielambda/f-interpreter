using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Utils;
using LanguageExt;
using System.Collections.Immutable;

namespace FCompiler.Semantic;

public record SemanticError(string message, Span span);

public class SemanticAnalyzer {
    private Scope _currentScope;
    private readonly List<SemanticError> _errors = [];

    public SemanticAnalyzer() {
        _currentScope = new Scope("global", Scope.ScopeType.Global);
        AddBuiltinFunctions();
    }

    public Either<ImmutableArray<SemanticError>, Unit> Analyze(Ast ast) {
        _errors.Clear();

        foreach (var element in ast.elements)
            VisitElement(element);

        return _errors.Count == 0
            ? Unit.Default
            : _errors.ToImmutableArray();
    }

    private void AddBuiltinFunctions() {
        var builtins = new[] {
            "plus", "minus", "times", "divide",
            "head", "tail", "cons",
            "equal", "nonequal", "less", "lesseq", "greater", "greatereq",
            "isint", "isreal", "isbool", "isnull", "isatom", "islist",
            "and", "or", "xor", "not", "eval",
        };

        foreach (var builtin in builtins)
            _currentScope.TryAddSymbol(builtin, new FunctionInfo([], new Span(0, 0, 0)));
    }

    private void VisitElement(Element element) {
        switch (element) {
            case ElementList list:
                VisitList(list);
                break;
            case ElementQuote quote:
                VisitQuote(quote);
                break;
            case ElementIdentifier ident:
                VisitIdentifier(ident);
                break;
            case ElementKeyword keyword:
                VisitKeyword(keyword);
                break;
            case ElementNull:
            case ElementInteger:
            case ElementReal:
            case ElementBool:
                break;
        }
    }

    private void VisitList(ElementList list) {
        if (list.elements.Count == 0) return;

        var first = list.elements[0];
        if (first is ElementKeyword keyword) {
            HandleSpecialForm(keyword, list);
        } else {
            foreach (var element in list.elements)
                VisitElement(element);

            if (first is ElementIdentifier ident)
                CheckIdentifierUsage(ident.identifier.value, ident.identifier.span);
        }
    }

    private void HandleSpecialForm(ElementKeyword keyword, ElementList list) {
        switch (keyword.keyword.type) {
            case Keyword.Type.Setq:
                VisitSetq(list);
                break;
            case Keyword.Type.Func:
                VisitFunc(list);
                break;
            case Keyword.Type.Lambda:
                VisitLambda(list);
                break;
            case Keyword.Type.Prog:
                VisitProg(list);
                break;
            case Keyword.Type.Cond:
                VisitCond(list);
                break;
            case Keyword.Type.While:
                VisitWhile(list);
                break;
            case Keyword.Type.Return:
                VisitReturn(list);
                break;
            case Keyword.Type.Break:
                VisitBreak(list);
                break;
            case Keyword.Type.Quote:
                VisitQuoteForm(list);
                break;
            default:
                foreach (var element in list.elements)
                    VisitElement(element);
                break;
        }
    }

    private void VisitSetq(ElementList list) {
        if (list.elements is not [_, var identifier, var value]) {
            AddError("setq requires exactly 2 arguments");
            return;
        }

        if (identifier is ElementIdentifier ident) {
            var varName = ident.identifier.value;
            _currentScope.TryAddSymbol(varName, new VariableInfo(ident.identifier.span));

            VisitElement(value);
        } else {
            AddError("setq first argument must be an identifier");
        }
    }

    private void VisitFunc(ElementList list) {
        if (list.elements.Count != 4) {
            AddError("func requires exactly 3 arguments");
            return;
        }

        if (list.elements[1] is ElementIdentifier funcName) {
            var name = funcName.identifier.value;

            var parameters = new List<string>();
            if (list.elements[2] is ElementList paramList) {
                foreach (var param in paramList.elements)
                    if (param is ElementIdentifier paramIdent)
                        parameters.Add(paramIdent.identifier.value);
                    else
                        AddError("Function parameter must be an identifier");
            }

            if (!_currentScope.TryAddSymbol(name, new FunctionInfo(parameters, funcName.identifier.span)))
                AddError($"Function '{name}' is already declared", funcName.identifier.span);

            var functionScope = new Scope(name, Scope.ScopeType.Function, _currentScope);

            foreach (var param in parameters)
                functionScope.TryAddSymbol(param, new VariableInfo(new Span(0, 0, 0)));

            var oldScope = _currentScope;
            _currentScope = functionScope;

            VisitElement(list.elements[3]);

            _currentScope = oldScope;
        } else {
            AddError("func first argument must be an identifier");
        }
    }

    private void VisitLambda(ElementList list) {
        if (list.elements is not [_, var args, var body]) {
            AddError("lambda requires exactly 2 arguments");
            return;
        }

        var lambdaScope = new Scope("lambda", Scope.ScopeType.Function, _currentScope);

        if (args is ElementList argsList)
            foreach (var param in argsList.elements)
                if (param is ElementIdentifier paramIdent)
                    lambdaScope.TryAddSymbol(paramIdent.identifier.value, new VariableInfo(paramIdent.identifier.span));

        var oldScope = _currentScope;
        _currentScope = lambdaScope;

        VisitElement(body);

        _currentScope = oldScope;
    }

    private void VisitProg(ElementList list) {
        if (list.elements is not [_, var decls, ..var exprs]) {
            AddError("prog has to have at least 1 argument");
            return;
        }
        var progScope = new Scope("prog", Scope.ScopeType.Prog, _currentScope);

        if (decls is ElementList varList)
            foreach (var variable in varList.elements)
                if (variable is ElementIdentifier varIdent)
                    progScope.TryAddSymbol(
                        varIdent.identifier.value,
                        new VariableInfo(varIdent.identifier.span)
                    );

        var oldScope = _currentScope;
        _currentScope = progScope;

        exprs.ForEach(VisitElement);

        _currentScope = oldScope;
    }

    private void VisitWhile(ElementList list) {
        if (list.elements is not [_, var cond, var body]) {
            AddError("while requires exactly 2 arguments");
            return;
        }

        var loopScope = new Scope("while", Scope.ScopeType.Loop, _currentScope);

        var oldScope = _currentScope;
        _currentScope = loopScope;

        VisitElement(cond);
        VisitElement(body);

        _currentScope = oldScope;
    }

    private void VisitReturn(ElementList list) {
        if (!_currentScope.IsInFunctionContext())
            AddError("return can only be used inside a function");

        if (list.elements.Count > 1)
            VisitElement(list.elements[1]);
    }

    private void VisitBreak(ElementList list) {
        if (!_currentScope.IsInLoopContext())
            AddError("break can only be used inside a loop");
    }

    private void VisitQuote(ElementQuote quote) { }

    private void VisitQuoteForm(ElementList list) { }

    private void VisitCond(ElementList list) {
        foreach (var element in list.elements.Skip(1))
            VisitElement(element);
    }

    private void VisitIdentifier(ElementIdentifier ident) {
        CheckIdentifierUsage(ident.identifier.value, ident.identifier.span);
    }

    private void VisitKeyword(ElementKeyword keyword) { }

    private void CheckIdentifierUsage(string name, Span useSpan) {
        if (_currentScope.Lookup(name) is null)
            AddError($"Identifier '{name}' is not declared in current scope", useSpan);
    }

    private void AddError(string message, Span span) =>
        _errors.Add(new SemanticError(message, span));

    private void AddError(string message) =>
        AddError(message, new Span(0, 0, 0));
}