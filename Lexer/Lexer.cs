using FCompiler.Utils;
using LanguageExt;
using System.Text;

namespace FCompiler.Lexer;

public record struct LexerError(string message);

public class Lexer {
    public static IEnumerable<Either<LexerError, Token>> Lex(IEnumerable<string> lines) {
        var lexer = new Lexer();
        foreach (var line in lines) {
            lexer.SetNextLine(line);
            foreach (var token in lexer.Tokens())
                yield return token;
        }
    }

    private string _input = string.Empty;
    private Span _span = new Span(lineNumber: 0, begin: 0, end: 0);

    private Lexer() { }

    private void SetNextLine(string line) {
        _input = line;
        _span.lineNumber++;
        _span.begin = _span.end = 1;
    }

    private int Position => _span.end - 1;
    private char Current => Position < _input.Length ? _input[Position] : '\0';
    private char Peek => Position + 1 < _input.Length ? _input[Position + 1] : '\0';
    private void Advance() => _span.end++;

    private void SkipWhitespaceAndComments() {
        while (true) {
            while (char.IsWhiteSpace(Current))
                Advance();

            if (Current == ';') {
                Advance();
                while (Current != '\0' && Current != '\n')
                    Advance();
                continue;
            }

            break;
        }
    }

    private IEnumerable<Either<LexerError, Token>> Tokens() {
        while (true) {
            SkipWhitespaceAndComments();

            _span.begin = _span.end;

            if (Current == '\0')
                yield break;

            if (Current == '(') {
                Advance();
                yield return new Punctuation(Punctuation.Type.LParen, _span);
                continue;
            }

            if (Current == ')') {
                Advance();
                yield return new Punctuation(Punctuation.Type.RParen, _span);
                continue;
            }

            if (Current == '\'') {
                Advance();
                yield return new Punctuation(Punctuation.Type.QuoteOp, _span);
                continue;
            }

            if (char.IsLetter(Current)) {
                yield return LexIdentifier();
                continue;
            }

            if (char.IsDigit(Current) || ((Current == '+' || Current == '-') && char.IsDigit(Peek))) {
                yield return LexNumber();
                continue;
            }

            yield return new LexerError($"Unexpected character: {Current}");
            yield break;
        }
    }

    private Either<LexerError, Token> LexIdentifier() {
        var idBuilder = new StringBuilder();
        while (char.IsLetter(Current) || char.IsDigit(Current)) {
            idBuilder.Append(Current);
            Advance();
        }
        string id = idBuilder.ToString();

        return id switch {
            "true"   => new Bool(true,                   _span),
            "false"  => new Bool(false,                  _span),
            "null"   => new Keyword(Keyword.Type.Null,   _span),
            "quote"  => new Keyword(Keyword.Type.Quote,  _span),
            "setq"   => new Keyword(Keyword.Type.Setq,   _span),
            "func"   => new Keyword(Keyword.Type.Func,   _span),
            "lambda" => new Keyword(Keyword.Type.Lambda, _span),
            "cond"   => new Keyword(Keyword.Type.Cond,   _span),
            "prog"   => new Keyword(Keyword.Type.Prog,   _span),
            "while"  => new Keyword(Keyword.Type.While,  _span),
            "return" => new Keyword(Keyword.Type.Return, _span),
            "break"  => new Keyword(Keyword.Type.Break,  _span),
            _        => new Identifier(id,               _span)
        };
    }

    private Either<LexerError, Token> LexNumber() {
        var numBuilder = new StringBuilder();
        if (Current == '+' || Current == '-') {
            numBuilder.Append(Current);
            Advance();
        }

        while (char.IsDigit(Current)) {
            numBuilder.Append(Current);
            Advance();
        }

        var hasDot = false;
        if (Current == '.') {
            hasDot = true;
            numBuilder.Append(Current);
            Advance();

            if (!char.IsDigit(Current)) {
                return new LexerError("Expected digit after '.'");
            }

            while (char.IsDigit(Current)) {
                numBuilder.Append(Current);
                Advance();
            }
        }

        string numStr = numBuilder.ToString();
        if (hasDot) {
            if (double.TryParse(numStr, out double realValue))
                return new Real(realValue, _span);
            else
                return new LexerError($"Invalid real number: {numStr}");
        }
        else {
            if (int.TryParse(numStr, out int intValue))
                return new Integer(intValue, _span);
            else
                return new LexerError($"Invalid integer number: {numStr}");
        }
    }
}
