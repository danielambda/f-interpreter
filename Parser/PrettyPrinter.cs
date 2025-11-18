using LanguageExt;
using System.Text;

namespace FCompiler.Parser;

public static class PrettyPrinter {
    public static string PrettyPrint(this Ast ast) =>
        string.Join(
            '\n',
            ast.elements.Select(e => e.PrettyPrint())
        );

    public static string PrettyPrint(this Element element) =>
        new StringBuilder().AppendElement(element, 0, "", "").ToString().TrimEnd();

    private static string PrettyPrintList(List<Element> elements, int depth) => Offset(depth) + (elements switch {
        [] => "Empty List",
        _ => $"List:\n{string.Join("\n", elements.Select((e, i) =>
              (i == elements.Count - 1 ? "└── " : "├── ") + e.PrettyPrint().TrimStart()))}"
    });

    private static string Offset(int depth) => new string(' ', depth * 4);

    private static string IndentChild(string child, int depth) {
        return string.Join("\n", child.Split('\n').Select(line =>
            string.IsNullOrWhiteSpace(line) ? "" : $"{Offset(depth)}│           {line}"));
    }

    public static StringBuilder AppendElement(
        this StringBuilder sb,
        Element element,
        int indentLevel,
        string myPrefix,
        string childPrefix
    ) => element switch {
        Element.List(var els) when els.Count == 0 => sb.Append($"{myPrefix}Empty List"),
        Element.List(var els) => sb.Append($"{myPrefix}List:") .AppendChildren(els, indentLevel + 1, childPrefix),
        Element.Quote(var q)  => sb.Append($"{myPrefix}Quote:").AppendChildren([q], indentLevel + 1, childPrefix),
        Element.Identifier { identifier.value: var v }  => sb.Append($"{myPrefix}Identifier: {v}"),
        Element.SpecialForm { specialForm.type: var v } => sb.Append($"{myPrefix}SpecialForm: {v}"),
        Element.Null                                    => sb.Append($"{myPrefix}Null"),
        Element.Integer { integer.value: var v }        => sb.Append($"{myPrefix}Literal: {v}"),
        Element.Real { real.value: var v }              => sb.Append($"{myPrefix}Literal: {v}"),
        Element.Bool { boolean.value: var v }           => sb.Append($"{myPrefix}Literal: {v}"),
        _ => throw new ArgumentException($"Unknown element type: {element.GetType().Name}")
    };

    public static StringBuilder AppendChildren(
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
