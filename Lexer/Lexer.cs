using FCompiler.Utils;
using LanguageExt;
using System.Text;
using static FCompiler.Lexer.Token.Punctuation.Type;
using static FCompiler.Lexer.Token.SpecialForm.Type;

namespace FCompiler.Lexer;

public record struct LexerError(string message, Span span);

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
                yield return new Token.Punctuation(LParen, _span);
                continue;
            }

            if (Current == ')') {
                Advance();
                yield return new Token.Punctuation(RParen, _span);
                continue;
            }

            if (Current == '\'') {
                Advance();
                yield return new Token.Punctuation(QuoteOp, _span);
                continue;
            }

            if (char.IsLetter(Current) || Current is '_') {
                yield return LexIdentifier();
                continue;
            }

            if (char.IsDigit(Current) || ((Current == '+' || Current == '-') && char.IsDigit(Peek))) {
                yield return LexNumber();
                continue;
            }

            yield return new LexerError($"Unexpected character: {Current}", _span);
            yield break;
        }
    }

    private Either<LexerError, Token> LexIdentifier() {
        var idBuilder = new StringBuilder();
        while (  char.IsLetter(Current)
              || char.IsDigit(Current)
              || Current is '_' or '-' or '!' or '?') {
            idBuilder.Append(Current);
            Advance();
        }
        string id = idBuilder.ToString();

        return id switch {
            "true"   => new Token.Bool(true,          _span),
            "false"  => new Token.Bool(false,         _span),
            "null"   => new Token.Null(               _span),
            "quote"  => new Token.SpecialForm(Quote,  _span),
            "setq"   => new Token.SpecialForm(Setq,   _span),
            "func"   => new Token.SpecialForm(Func,   _span),
            "lambda" => new Token.SpecialForm(Lambda, _span),
            "cond"   => new Token.SpecialForm(Cond,   _span),
            "prog"   => new Token.SpecialForm(Prog,   _span),
            "while"  => new Token.SpecialForm(While,  _span),
            "return" => new Token.SpecialForm(Return, _span),
            "break"  => new Token.SpecialForm(Break,  _span),
            _        => new Token.Identifier(id,      _span)
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
                return new LexerError("Expected digit after '.'", _span);
            }

            while (char.IsDigit(Current)) {
                numBuilder.Append(Current);
                Advance();
            }
        }

        string numStr = numBuilder.ToString();
        if (hasDot) {
            if (double.TryParse(numStr, out double realValue))
                return new Token.Real(realValue, _span);
            else
                return new LexerError($"Invalid real number: {numStr}", _span);
        }
        else {
            if (int.TryParse(numStr, out int intValue))
                return new Token.Integer(intValue, _span);
            else
                return new LexerError($"Invalid integer number: {numStr}", _span);
        }
    }
}
