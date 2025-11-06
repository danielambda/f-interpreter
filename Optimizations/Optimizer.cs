using FCompiler.Lexer;
using FCompiler.Parser;
using FCompiler.Semantic;
using LanguageExt;
using System.Collections.Immutable;

namespace FCompiler.Optimizations;

public class Optimizer {
    private readonly Ast _ast;
    private readonly Dictionary<string, (FunctionInfo Info, List<Element> Body)> _functions;
    private readonly Dictionary<string, int> _variableUsageCount;

    public Optimizer(Ast ast) {
        _ast = ast;
        _functions = [];
        _variableUsageCount = [];
    }

    public Ast Optimize() {
        CollectFunctionDefinitions();
        CollectVariableUsage();

        // Применение оптимизаций
        var optimizedElements = _ast.elements.Select(OptimizeElement).ToList();

        // Удаление неиспользуемых переменных
        optimizedElements = RemoveUnusedVariables(optimizedElements);

        // Инлайнинг функций
        optimizedElements = InlineFunctions(optimizedElements);

        return new Ast(optimizedElements);
    }

    private void CollectFunctionDefinitions() {
        foreach (var element in _ast.elements) {
            if (element is ElementList {
                elements:
                  [ ElementKeyword { keyword.type: Keyword.Type.Func }
                  , ElementIdentifier funcName
                  , ElementList paramList
                  , ..var body
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

    private void CollectVariableUsage() =>
        _ast.elements.ForEach(CountVariableUsage);

    private void CountVariableUsage(Element element) {
        switch (element) {
            case ElementList { elements: [ElementKeyword { keyword.type: Keyword.Type.Setq }, _, var value] }:
                CountVariableUsage(value);
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

        // Оптимизация арифметических операций с константами
        if (optimizedElements.Count == 3) {
            if (TryOptimizeArithmetic(optimizedElements) is {} result)
                return result;
        }

        return new ElementList(optimizedElements);
    }

    private ElementList? TryOptimizeArithmetic(List<Element> elements)
    {
        if (elements[0] is ElementIdentifier operation &&
            elements[1] is ElementInteger i1 &&
            elements[2] is ElementInteger i2)
        {
            var result = OptimizeArithmetic(operation.identifier.value, i1.integer.value, i2.integer.value);
            if (result.HasValue)
                return new ElementList(new List<Element> { new ElementInteger(new Integer(result.Value, i1.integer.span)) });
        }

        if (elements[0] is ElementIdentifier operation2 &&
            elements[1] is ElementReal r1 &&
            elements[2] is ElementReal r2)
        {
            var result = OptimizeArithmetic(operation2.identifier.value, r1.real.value, r2.real.value);
            if (result.HasValue)
                return new ElementList(new List<Element> { new ElementReal(new Real(result.Value, r1.real.span)) });
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

    private List<Element> RemoveUnusedVariables(List<Element> elements) {
        // Console.WriteLine($"RemoveUnusedVariables {elements.Count}");
        var result = new List<Element>();

        foreach (var element in elements) {
            if (element is ElementList {
                elements:
                  [ ElementKeyword { keyword.type: Keyword.Type.Setq }
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
                // Оптимизация объявлений переменных в prog
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
                optimizedList.AddRange(RemoveUnusedVariables(rest).Select(OptimizeElement));
                result.Add(new ElementList(optimizedList));
            } else {
                result.Add(element);
            }
        }

        return result;
    }

    private List<Element> InlineFunctions(List<Element> elements) {
        var result = new List<Element>();

        foreach (var element in elements) {
            // Console.WriteLine($"TryInlineFunctionCall {element}");
            if (TryInlineFunctionCall(element) is {} inlined) {
                result.AddRange(inlined);
            } else {
                result.Add(element);
            }
        }

        return result;
    }

    private List<Element>? TryInlineFunctionCall(Element element) =>
        element is ElementList { elements: [ElementIdentifier ident, ..var args] } list
        && _functions.TryGetValue(ident.identifier.value, out var func)
        && args.Count == func.Info.parameters.Count
          ? PerformFunctionInlining(args, func.Body, func.Info)
          : null;

    private List<Element> PerformFunctionInlining(List<Element> args, List<Element> funcBody, FunctionInfo funcInfo) {
        // Console.WriteLine("PerformFunctionInlining");
        var paramMap = funcInfo.parameters.Zip(args).ToDictionary();
        return funcBody
            .Select(e => ReplaceParameters(e, paramMap))
            .ToList();
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
