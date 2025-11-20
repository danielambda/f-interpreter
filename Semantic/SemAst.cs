using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Utils;

namespace FCompiler.Semantic;

public static class Sem {
    public abstract record Elem;
    public abstract record Expr : Elem;

    public record Ast(List<Elem> elements);

    public record Setq(Identifier name, Expr body)                               : Expr;
    public record Fun(Identifier name, List<Identifier> args, Expr body)         : Elem;
    public record Lambda(List<Identifier> args, Expr body)                       : Expr;
    public record Prog(List<Identifier> vars, List<Elem> body, Expr last)        : Expr;
    public record Cond(Expr cond, Expr t, Expr? f = null)                        : Expr;
    public record While(Expr cond, Expr body)                                    : Expr;
    public record Return(Expr value)                                             : Expr;
    public record Break                                                          : Elem {
        public static Break Default = new Break();
    }
    public record FunApp(Expr fun, List<Expr> args)                              : Expr;
    public record Quote(Element element)                                         : Expr;
    public record Integer(Element.Integer integer)                               : Expr;
    public record Real(Element.Real real)                                        : Expr;
    public record Bool(Element.Bool boolean)                                     : Expr;
    public record Null(Element.Null elementNull)                                 : Expr;
    public record Identifier(Element.Identifier identifier)                      : Expr;

    public static FCompiler.Parser.Ast ToAst(this Ast semAst) =>
        new FCompiler.Parser.Ast(semAst.elements.Map(ToElement).ToList());

    public static FCompiler.Parser.Element ToElement(this Elem elem) => elem switch {
        Setq(var name, var body) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Setq, new Span())),
            name.ToElement(),
            body.ToElement(),
        ]),
        Fun(var name, var args, var body) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Func, new Span())),
            name.ToElement(),
            ..args.Map(ToElement),
            body.ToElement(),
        ]),
        Lambda(var args, var body) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Lambda, new Span())),
            ..args.Map(ToElement),
            body.ToElement(),
        ]),
        Prog(var vars, var args, var last) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Prog, new Span())),
            new Element.List(vars.Map(ToElement).ToList()),
            ..args.Map(ToElement),
            last.ToElement(),
        ]),
        Cond(var cond, var t, null) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Cond, new Span())),
            cond.ToElement(),
            t.ToElement(),
        ]),
        Cond(var cond, var t, Expr f) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Cond, new Span())),
            cond.ToElement(),
            t.ToElement(),
            f.ToElement(),
        ]),
        While(var cond, var body) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.While, new Span())),
            cond.ToElement(),
            body.ToElement(),
        ]),
        Return(var value) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Return, new Span())),
            value.ToElement(),
        ]),
        Break => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Return, new Span())),
        ]),
        FunApp(var fun, var args) => new Element.List([
            fun.ToElement(),
            ..args.Map(ToElement)
        ]),
        Quote(var value) => new Element.List([
            new Element.SpecialForm(new Token.SpecialForm(Token.SpecialForm.Type.Quote, new Span())),
            value
        ]),
        Integer(var v) => v,
        Real(var v) => v,
        Bool(var v) => v,
        Null(var v) => v,
        Identifier(var v) => v,
        _ => throw new ArgumentException($"Unknown element type: {elem.GetType().Name}")
    };

    public static Span? TryGetSpan(this Elem elem) => elem switch {
        Setq(var name, _) => name.TryGetSpan(),
        Fun(var name, _, _) => name.TryGetSpan(),
        Lambda(var args, var body) =>
            args.FirstOrDefault()?.TryGetSpan() ?? body.TryGetSpan(),
        Prog(var vars, var args, var last) =>
            vars.FirstOrDefault()?.TryGetSpan() ?? args.FirstOrDefault()?.TryGetSpan() ?? last.TryGetSpan(),
        Cond(var cond, var t, var f) => cond.TryGetSpan() ?? t.TryGetSpan() ?? f?.TryGetSpan(),
        While(var cond, var body) => cond.TryGetSpan() ?? body.TryGetSpan(),
        Return(var value) => value.TryGetSpan(),
        Break => null,
        FunApp(var fun, var args) => fun.TryGetSpan() ?? args.FirstOrDefault()?.TryGetSpan(),
        Quote(var value) => null,
        Integer(var v) => v.integer.span,
        Real(var v) => v.real.span,
        Bool(var v) => v.boolean.span,
        Null(var v) => v.@null.span,
        Identifier(var v) => v.identifier.span,
        _ => throw new ArgumentException($"Unknown element type: {elem.GetType().Name}")
    };
}
