using FCompiler.Lexer;
using FCompiler.Utils;
using LanguageExt;
using static FCompiler.Lexer.Token.Punctuation.Type;

namespace FCompiler.Parser;

public abstract record ParserError {
    public record ExpectedRParen(Span span): ParserError {
        public override string ToString() =>
            $"Expected ')' at {span.PrettyPrint()}";
    }

    public record UnexpectedRParen(Span span): ParserError {
        public override string ToString() =>
            $"Unexpected ')' at {span.PrettyPrint()}";
    }

    public record NoTokens : ParserError {
        public static NoTokens Instance { get; } = new NoTokens();
    }
}

public class Parser {
    private readonly IEnumerator<Token> _tokenEnumerator;
    private Token? _current;
    private void Advance() =>
        _current = _tokenEnumerator.MoveNext()
          ? _tokenEnumerator.Current
          : null;

    public static Either<ParserError, Ast> Parse(IEnumerable<Token> tokens) =>
        new Parser(tokens).ParseProgram();

    private Parser(IEnumerable<Token> tokens) {
        _tokenEnumerator = tokens.GetEnumerator();
        Advance();
    }

    private Either<ParserError, Ast> ParseProgram() {
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

    private Either<ParserError, Element> ParseElement() {
        var prev = _current;
        Advance();
        return prev switch {
            Token.Punctuation{ type: RParen, span: var span } => new ParserError.UnexpectedRParen(span),
            Token.Punctuation{ type: LParen, span: var span } => ParseList(span).Map(a => (Element)(new Element.List(a))),
            Token.Punctuation{ type: QuoteOp } => ParseElement().Map(a => (Element)(new Element.Quote(a))),
            Token.Identifier a  => new Element.Identifier(a),
            Token.Null a        => new Element.Null(a),
            Token.SpecialForm a => new Element.SpecialForm(a),
            Token.Integer a     => new Element.Integer(a),
            Token.Real a        => new Element.Real(a),
            Token.Bool a        => new Element.Bool(a),
            null                => ParserError.NoTokens.Instance,
            _ => throw new InvalidProgramException("unreachable")
        };
    }

    private Either<ParserError, List<Element>> ParseList(Span LParenspan) {
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
        return new ParserError.ExpectedRParen(LParenspan);
    }
}
