using FCompiler.Lexer;
using FCompiler.Utils;
using LanguageExt;
using static FCompiler.Lexer.Token.Punctuation.Type;

namespace FCompiler.Parser;

public record ParserError(string message, Span span);

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
            Token.Punctuation{ type: RParen, span: var span } => new ParserError($"Unexpected )", span),
            Token.Punctuation{ type: LParen, span: var span } => ParseList(span).Map(a => (Element)(new Element.List(a))),
            Token.Punctuation{ type: QuoteOp } => ParseElement().Map(a => (Element)(new Element.Quote(a))),
            Token.Identifier a  => new Element.Identifier(a),
            Token.Null a        => new Element.Null(a),
            Token.SpecialForm a => new Element.SpecialForm(a),
            Token.Integer a     => new Element.Integer(a),
            Token.Real a        => new Element.Real(a),
            Token.Bool a        => new Element.Bool(a),
            null or _ => throw new InvalidProgramException("unreachable")
        };
    }

    public Either<ParserError, List<Element>> ParseList(Span LParenspan) {
        List<Element> elements = [];
        ParserError? error = null;
        while (_current is not null && error is null) {
            if (_current is Token.Punctuation{ type: RParen }) {
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
