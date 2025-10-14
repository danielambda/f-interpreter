using FCompiler.Utils;
using FCompiler.Lexer;
using LanguageExt;

namespace FCompiler.Parser;

public record ParserError(string message, Span span);

public record Ast(List<Element> elements) {
    public string PrettyPrint() =>
        string.Join(
            '\n',
            elements.Select(e => e.PrettyPrint(0))
        );
}

public abstract record Element {
    public string PrettyPrint(int depth) => this switch {
        ElementList(var es) => $"\n{new string('\t', depth)}({string.Join(", ", es.Select(e => e.PrettyPrint(depth + 1)))})",
        ElementQuote(var e) => e.PrettyPrint(depth),
        ElementIdentifier(Identifier{ value: var value }) => $"Identifier {value}",
        ElementKeyword(Keyword{ type: var type }) => $"Keyword {type}",
        ElementNull                                 =>  "Literal null",
        ElementInteger(Integer{ value: var value }) => $"Literal {value.ToString()}",
        ElementReal(Real{ value: var value })       => $"Literal {value.ToString()}",
        ElementBool(Bool{ value: var value })       => $"Literal {value.ToString()}",
        _ => throw new InvalidProgramException("unreachable"),
    };
}
public record ElementList(List<Element> elements)      : Element;
public record ElementQuote(Element quote)              : Element;
public record ElementIdentifier(Identifier Identifier) : Element;
public record ElementKeyword(Keyword keyword)          : Element;
public record ElementNull(Span span)                   : Element;
public record ElementInteger(Integer integer)          : Element;
public record ElementReal(Real real)                   : Element;
public record ElementBool(Bool boolean)                : Element;

public class Parser {
    private readonly IEnumerator<Token> _tokenEnumerator;
    private Token? _current;
    private void Advance() =>
        _current = _tokenEnumerator.MoveNext()
          ? _tokenEnumerator.Current
          : null;

    public Parser(IEnumerable<Token> tokens) {
        _tokenEnumerator = tokens.GetEnumerator();
        Advance();
    }

    public Either<ParserError, Ast> ParseProgram() {
        List<Element> elements = [];
        ParserError? error = null;
        while (_current is not null && error is null) {
            ParseElement().Match(
                Left: a => error = a,
                Right: elements.Add
            );
        }

        return error is not null
            ? error
            : new Ast(elements);
    }

    public Either<ParserError, Element> ParseElement() {
        var prev = _current;
        Advance();
        return prev switch {
            Punctuation{ type: Punctuation.Type.RParen, span: var span } => new ParserError($"Unexpected )", span),
            Punctuation{ type: Punctuation.Type.LParen, span: var span } => ParseList(span).Map(a => (Element)(new ElementList(a))),
            Punctuation{ type: Punctuation.Type.QuoteOp } => ParseElement().Map(a => (Element)(new ElementQuote(a))),
            Identifier a => new ElementIdentifier(a),
            Keyword{ type: Keyword.Type.Null, span: var span } => new ElementNull(span),
            Keyword a => new ElementKeyword(a),
            Integer a => new ElementInteger(a),
            Real a => new ElementReal(a),
            Bool a => new ElementBool(a),
            null or _ => throw new InvalidProgramException("unreachable")
        };
    }

    public Either<ParserError, List<Element>> ParseList(Span LParenspan) {
        List<Element> elements = [];
        ParserError? error = null;
        while (_current is not null && error is null) {
            if (_current is Punctuation{ type: Punctuation.Type.RParen }) {
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
