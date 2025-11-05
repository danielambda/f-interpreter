using FCompiler.Lexer;
using FCompiler.Utils;
using LanguageExt;
using System.Text;

namespace FCompiler.Parser;

public record ParserError(string message, Span span);

public record Ast(List<Element> elements) {
    public string PrettyPrint() =>
        string.Join(
            '\n',
            elements.Select(e => e.PrettyPrint(0))
        );
}

public static class PrettyPrinter {
    public static string PrettyPrint(Element element) =>
        new StringBuilder().AppendElement(element, 0, "", "").ToString().TrimEnd();

    private static StringBuilder AppendElement(
        this StringBuilder sb,
        Element element,
        int indentLevel,
        string myPrefix,
        string childPrefix
    ) => element switch {
        ElementList(var els) when els.Count == 0 => sb.Append($"{myPrefix}Empty List"),
        ElementList(var els) => sb.Append($"{myPrefix}List:") .AppendChildren(els, indentLevel + 1, childPrefix),
        ElementQuote(var q)  => sb.Append($"{myPrefix}Quote:").AppendChildren([q], indentLevel + 1, childPrefix),
        ElementIdentifier { identifier.value: var v } => sb.Append($"{myPrefix}Identifier: {v}"),
        ElementKeyword { keyword.type: var v }        => sb.Append($"{myPrefix}Keyword: {v}"),
        ElementNull                                   => sb.Append($"{myPrefix}Null"),
        ElementInteger { integer.value: var v }       => sb.Append($"{myPrefix}Literal: {v}"),
        ElementReal { real.value: var v }             => sb.Append($"{myPrefix}Literal: {v}"),
        ElementBool { boolean.value: var v }          => sb.Append($"{myPrefix}Literal: {v}"),
        _ => throw new ArgumentException($"Unknown element type: {element.GetType().Name}")
    };

    private static StringBuilder AppendChildren(
        this StringBuilder sb,
        List<Element> children,
        int indentLevel,
        string prefix
    ) {
        var childPrefix = indentLevel == 0 ? "" : prefix + "│   ";
        var newPrefix   = indentLevel == 0 ? "" : prefix + "├── ";
        for (int i = 0; i < children.Count - 1; i++) {
            sb.AppendLine()
              .AppendElement(children[i], indentLevel + 1, newPrefix, childPrefix);
        }
        childPrefix = indentLevel == 0 ? "" : prefix + "    ";
        newPrefix   = indentLevel == 0 ? "" : prefix + "└── ";
        sb.AppendLine()
          .AppendElement(children.Last(), indentLevel + 1, newPrefix, childPrefix);
        return sb;
    }
}

public abstract record Element {
    public string PrettyPrint(int depth) => PrettyPrinter.PrettyPrint(this);
    private static string PrettyPrintList(List<Element> elements, int depth) => elements switch {
        [] => $"{Offset(depth)}Empty List",
        _ => $"{Offset(depth)}List:\n{string.Join("\n", elements.Select((e, i) =>
            $"{Offset(depth)}{(i == elements.Count - 1 ? "└── " : "├── ")}{e.PrettyPrint(depth + 1).TrimStart()}"))}"
    };

    private static string Offset(int depth) => new string(' ', depth * 4);

    private static string IndentChild(string child, int depth) {
        return string.Join("\n", child.Split('\n').Select(line =>
            string.IsNullOrWhiteSpace(line) ? "" : $"{Offset(depth)}│           {line}"));
    }
}

public record ElementList(List<Element> elements)      : Element;
public record ElementQuote(Element quote)              : Element;
public record ElementIdentifier(Identifier identifier) : Element;
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
