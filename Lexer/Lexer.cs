using LanguageExt;
using System.Text;

namespace FCompiler.Lexer;

public record struct LexerError(string message);

public class Lexer
{
    private string _input = string.Empty;
    private int _position;

    public void SetInput(string input)
    {
        _input = input;
        _position = 0;
    }

    private char Current => _position < _input.Length ? _input[_position] : '\0';
    private char Peek => _position + 1 < _input.Length ? _input[_position + 1] : '\0';
    private void Advance() => _position++;

    private void SkipWhitespaceAndComments()
    {
        while (true)
        {
            while (char.IsWhiteSpace(Current))
                Advance();

            if (Current == ';')
            {
                Advance();
                while (Current != '\0' && Current != '\n')
                    Advance();
                continue;
            }

            break;
        }
    }

    public IEnumerable<Either<LexerError, Token>> Tokens()
    {
        while (true)
        {
            SkipWhitespaceAndComments();

            if (Current == '\0')
                yield break;

            if (Current == '(')
            {
                Advance();
                yield return new Token(TokenType.LParen);
                continue;
            }

            if (Current == ')')
            {
                Advance();
                yield return new Token(TokenType.RParen);
                continue;
            }

            if (Current == '\'')
            {
                Advance();
                yield return new Token(TokenType.QuoteOp);
                continue;
            }

            if (char.IsLetter(Current))
            {
                var idBuilder = new StringBuilder();
                while (char.IsLetter(Current) || char.IsDigit(Current))
                {
                    idBuilder.Append(Current);
                    Advance();
                }
                string id = idBuilder.ToString();

                yield return id switch
                {
                    "true" => new Token(TokenType.Boolean, true),
                    "false" => new Token(TokenType.Boolean, false),
                    "null" => new Token(TokenType.Null, null),
                    "quote" => new Token(TokenType.Quote),
                    "setq" => new Token(TokenType.Setq),
                    "func" => new Token(TokenType.Func),
                    "lambda" => new Token(TokenType.Lambda),
                    "cond" => new Token(TokenType.Cond),
                    "prog" => new Token(TokenType.Prog),
                    "while" => new Token(TokenType.While),
                    "return" => new Token(TokenType.Return),
                    "break" => new Token(TokenType.Break),
                    _ => new Token(TokenType.Identifier, id)
                };
                continue;
            }

            if (char.IsDigit(Current) || ((Current == '+' || Current == '-') && char.IsDigit(Peek)))
            {
                var numBuilder = new StringBuilder();
                if (Current == '+' || Current == '-')
                {
                    numBuilder.Append(Current);
                    Advance();
                }

                while (char.IsDigit(Current))
                {
                    numBuilder.Append(Current);
                    Advance();
                }

                var hasDot = false;
                if (Current == '.')
                {
                    hasDot = true;
                    numBuilder.Append(Current);
                    Advance();

                    if (!char.IsDigit(Current))
                    {
                        yield return new LexerError("Expected digit after '.'");
                        yield break;
                    }

                    while (char.IsDigit(Current))
                    {
                        numBuilder.Append(Current);
                        Advance();
                    }
                }

                string numStr = numBuilder.ToString();
                if (hasDot)
                {
                    if (double.TryParse(numStr, out double realValue))
                        yield return new Token(TokenType.Real, realValue);
                    else
                        yield return new LexerError($"Invalid real number: {numStr}");
                }
                else
                {
                    if (int.TryParse(numStr, out int intValue))
                        yield return new Token(TokenType.Integer, intValue);
                    else
                        yield return new LexerError($"Invalid integer number: {numStr}");
                }
                continue;
            }

            yield return new LexerError($"Unexpected character: {Current}");
            yield break;
        }
    }
}