using FCompiler.Utils;
using FCompiler.Lexer;
using LanguageExt;

namespace FCompiler.Parser;

public record ParserError(string message, Span span);

public record Ast(List<Element> elements);
public abstract record Element;

public record ElementList(List<Element> elements)      : Element;
public record ElementQuote(Element quote)              : Element;
public record ElementIdentifier(Identifier Identifier) : Element;
public record ElementKeyword(Keyword keyword)          : Element;
public record ElementNull(Span span)                   : Element;
public record ElementInteger(Integer integer)          : Element;
public record ElementReal(Real real)                   : Element;
public record ElementBool(Bool boolean)                : Element;

public class Parser {
    private Token[] _tokens;
    private int _position = 0;

    private Token? Current => _position < _tokens.Length ? _tokens[_position] : null;
    private void Advance() => _position++;

    public Parser(Token[] tokens) =>
        _tokens = tokens;

    public Either<ParserError, Ast> ParseProgram() {
        List<Element> elements = [];
        ParserError? error = null;
        while (_tokens.Length > 0 && error is null) {
            ParseElement().Match(
                Left: a => error = a,
                Right: elements.Add
            );
        }
        if (error is not null) {
            return error;
        }
        return new Ast(elements);
    }

    public Either<ParserError, Element> ParseElement() {
        Advance();
        return Current switch {
            Punctuation{ type: Punctuation.Type.RParen, span: var span } => new ParserError($"Unexpected )", span),
            Punctuation{ type: Punctuation.Type.LParen, span: var span } => ParseList(span).Map(a => (Element)(new ElementList(a))),
            Punctuation{ type: Punctuation.Type.QuoteOp } => ParseElement().Map(a => (Element)(new ElementQuote(a))),
            Identifier a => (Element)(new ElementIdentifier(a)),
            Keyword{ type: Keyword.Type.Null, span: var span } => (Element)(new ElementNull(span)),
            Keyword a => (Element)(new ElementKeyword(a)),
            Integer a => (Element)(new ElementInteger(a)),
            Real a => (Element)(new ElementReal(a)),
            Bool a => (Element)(new ElementBool(a)),
            _ => throw new InvalidProgramException("unreachable")
        };
    }

    public Either<ParserError, List<Element>> ParseList(Span LParenspan) {
        List<Element> elements = [];
        ParserError? error = null;
        while (_tokens.Length > 0 && error is null) {
            if (Current is Punctuation{ type: Punctuation.Type.RParen }) {
                Advance();
                return elements;
            }

            ParseElement().Match(
                Left: a => error = a,
                Right: elements.Add
            );
        }
        if (error is not null) {
            return error;
        }
        return new ParserError($"Expected )", LParenspan);
    }
}
