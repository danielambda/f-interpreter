using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using LanguageExt;
using System.Collections.Immutable;

namespace FCompiler.Optimizations;

public class Optimizer {
    private readonly Ast _ast;
    private readonly Dictionary<string, (FunctionInfo Info, Element Body)> _functions;
    private readonly Dictionary<string, int> _variableUsageCount;

    public Optimizer(Ast ast) {
        _ast = ast;
        _functions = [];
        _variableUsageCount = [];
    }

    public Ast Optimize() {
        CollectFunctionDefinitions(_ast.elements);
        CollectVariableUsage(_ast.elements);

        var optimizedElements = _ast.elements.Select(OptimizeElement).ToList();

        optimizedElements = RemoveUnusedDecls(optimizedElements);
        optimizedElements = InlineFunctions(optimizedElements);

        _functions.Clear();
        _variableUsageCount.Clear();
        CollectFunctionDefinitions(optimizedElements);
        CollectVariableUsage(optimizedElements);

        optimizedElements = RemoveUnusedDecls(optimizedElements);
        optimizedElements = optimizedElements.Select(OptimizeElement).ToList();

        return new Ast(optimizedElements);
    }

    private void CollectFunctionDefinitions(List<Element> elements) {
        foreach (var element in elements) {
            if (element is ElementList {
                elements:
                  [ ElementKeyword { keyword.type: Keyword.Type.Func }
                  , ElementIdentifier funcName
                  , ElementList paramList
                  , var body
                  ]
                }
            ) {
                // TODO casting logic here is not totally correct
                var parameters = paramList.elements
                    .OfType<ElementIdentifier>()
                    .Select(param => param.identifier.value)
                    .ToList();
                _functions[funcName.identifier.value] =
                    ( new FunctionInfo(parameters, funcName.identifier.span)
                    , body
                    );
            }
        }
    }

    private void CollectVariableUsage(List<Element> elements) =>
        elements.ForEach(CountVariableUsage);

    private void CountVariableUsage(Element element) {
        switch (element) {
            case ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Setq }, _, var value] }:
                CountVariableUsage(value);
                break;
            case ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Func }, _, _, ..var body] }:
                body.ForEach(CountVariableUsage);
                break;
            case ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Prog }, _, ..var rest] }:
                rest.ForEach(CountVariableUsage);
                break;
            case ElementList list:
                list.elements.ForEach(CountVariableUsage);
                break;
            case ElementIdentifier ident:
                var varName = ident.identifier.value;
                _variableUsageCount[varName] = _variableUsageCount.GetValueOrDefault(varName) + 1;
                break;
        }
    }

    private Element OptimizeElement(Element element) => element switch {
        ElementList list => OptimizeList(list),
        _ => element
    };

    private ElementList OptimizeList(ElementList list) {
        var optimizedElements = list.elements.Select(OptimizeElement).ToList();

        if (optimizedElements.Count == 3) {
            if (TryOptimizeArithmetic(optimizedElements) is {} result)
                return result;
        }

        return new ElementList(optimizedElements);
    }

    private ElementList? TryOptimizeArithmetic(List<Element> elements) {
        if (elements is
            [ ElementIdentifier operation
            , ElementInteger i1
            , ElementInteger i2
            ]
        ) {
            var result = OptimizeArithmetic(operation.identifier.value, i1.integer.value, i2.integer.value);
            if (result.HasValue)
                return new ElementList([new ElementInteger(new Integer(result.Value, i1.integer.span))]);
        }

        if (elements is
            [ ElementIdentifier operation2
            , ElementReal r1
            , ElementReal r2
            ]
        ) {
            var result = OptimizeArithmetic(operation2.identifier.value, r1.real.value, r2.real.value);
            if (result.HasValue)
                return new ElementList([new ElementReal(new Real(result.Value, r1.real.span))]);
        }

        return null;
    }

    private double? OptimizeArithmetic(string operation, double a, double b) => operation switch {
        "plus" => a + b,
        "minus" => a - b,
        "times" => a * b,
        "divide" when b != 0 => a / b,
        _ => null
    };

    private long? OptimizeArithmetic(string operation, long a, long b) => operation switch {
        "plus" => a + b,
        "minus" => a - b,
        "times" => a * b,
        "divide" when b != 0 => a / b,
        _ => null
    };

    private List<Element> RemoveUnusedDecls(List<Element> elements) {
        var result = new List<Element>();

        foreach (var element in elements) {
            if (element is ElementList {
                elements:
                  [ ElementKeyword { keyword.type: Keyword.Type.Setq or Keyword.Type.Func }
                  , ElementIdentifier varIdent
                  , ..
                  ]
                }
            ) {
                if (_variableUsageCount.GetValueOrDefault(varIdent.identifier.value) > 0)
                    result.Add(element);
            } else if (element is ElementList {
                elements:
                  [ ElementKeyword { keyword.type: Keyword.Type.Prog } prog
                  , ElementList { elements: var progArgs }
                  , ..var rest
                  ]
                }
            ) {
                var optimizedVars = new List<Element>();
                foreach (var varElement in progArgs) {
                    if (varElement is ElementIdentifier progVarIdent) {
                        var varName = progVarIdent.identifier.value;
                        if (_variableUsageCount.GetValueOrDefault(varName) > 0) {
                            optimizedVars.Add(varElement);
                        }
                    } else {
                        optimizedVars.Add(varElement);
                    }
                }

                var optimizedList = new List<Element> { prog, new ElementList(optimizedVars) };
                optimizedList.AddRange(RemoveUnusedDecls(rest).Select(OptimizeElement));
                result.Add(new ElementList(optimizedList));
            } else {
                result.Add(element);
            }
        }

        return result;
    }

    private List<Element> InlineFunctions(List<Element> elements) {
        return elements.Map(Inline).ToList();

        Element Inline(Element element) => element switch {
            ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Setq } setq, var indent, var value] } =>
                new ElementList([setq, indent, Inline(value)]),
            ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Prog } prog, var scope, ..var body] } =>
                new ElementList([prog, scope, ..InlineFunctions(body)]),
            ElementList elementList when TryInlineFunctionCall(elementList) is {} inlined =>
                inlined,
            _ => element
        };
    }

    private Element? TryInlineFunctionCall(ElementList element) {
        var res =
            element.elements is [ElementIdentifier ident, ..var args] list
            && _functions.TryGetValue(ident.identifier.value, out var func)
            && args.Count == func.Info.parameters.Count
              ? BetaReduce(args, func.Body, func.Info)
              : null;

        return res;
    }

    private Element BetaReduce(List<Element> args, Element funcBody, FunctionInfo funcInfo) {
        // TODO handle names collision
        var paramMap = funcInfo.parameters.Zip(args).ToDictionary();
        return ReplaceParameters(funcBody, paramMap);
    }

    private Element ReplaceParameters(
        Element element,
        Dictionary<string, Element> paramMap
    ) => element switch {
        ElementIdentifier ident when paramMap.ContainsKey(ident.identifier.value) =>
            paramMap[ident.identifier.value],
        ElementList list =>
            new ElementList(list.elements.Select(e => ReplaceParameters(e, paramMap)).ToList()),
        _ => element
    };
}
