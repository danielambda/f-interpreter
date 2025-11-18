using FCompiler.Lexer;

namespace FCompiler.Parser;

public record Ast(List<Element> elements);

public abstract record Element {
    public record List(List<Element> elements)                : Element;
    public record Quote(Element quote)                        : Element;
    public record Identifier(Token.Identifier identifier)     : Element;
    public record SpecialForm(Token.SpecialForm specialForm)  : Element;
    public record Null(Token.Null @null)                       : Element;
    public record Integer(Token.Integer integer)              : Element;
    public record Real(Token.Real real)                       : Element;
    public record Bool(Token.Bool boolean)                    : Element;
}
