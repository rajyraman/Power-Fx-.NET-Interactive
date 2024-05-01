using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Utility;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Intellisense;
using Microsoft.PowerFx.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    public class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, IKernelCommandHandler<RequestValue>, IKernelCommandHandler<SendValue>, IKernelCommandHandler<RequestCompletions>
    {
        private static RecalcEngine _engine;
        private List<string> _identifiers;

        public PowerFxKernel(RecalcEngine engine) : base("powerfx")
        {
#if DEBUG
            if (!Debugger.IsAttached) Debugger.Launch();
#endif
            KernelInfo.LanguageName = "powerfx";
            KernelInfo.Description = "This Kernel can evaluate Power Fx snippets.";
            KernelInfo.LanguageVersion = typeof(RecalcEngine).Assembly.GetName().Version.ToString();
            _engine = engine;
        }

        public static RecalcEngine GetRecalcEngine() => _engine;

        public Task HandleAsync(SubmitCode code, KernelInvocationContext context)
        {
            var powerFxResult = new PowerFxExpression(_engine, code.Code).Evaluate();
            _identifiers = powerFxResult.Identifiers;

            var result = powerFxResult.Result.Select(x => $"<tr><td>{x.formula.FormatInput()}</td><td>{x.result.FormatOutput()}</td>");
            var stateCheckResult = powerFxResult.StateCheck.Select(x => $"<tr><td>{x.name.FormatInput()}</td><td>{x.result.FormatOutput()}</td></tr>");
            var stateCheckMarkdown = $"<tr><th>Variable</th><th>Result</th></tr>{string.Join("\n", stateCheckResult)}";
            if (code.Code == "?")
            {
                context.DisplayAs(stateCheckMarkdown.Table(), "text/markdown");
                foreach (var item in powerFxResult.StateCheck)
                {
                    DisplayStateCheck(context, item);
                }
            }
            else
            {
                var evaluationMarkdown = $"<tr><th>Expression</th><th>Result</th></tr>{"\n"}{string.Join("\n", result)}";
                context.DisplayAs(evaluationMarkdown.Table(), "text/markdown");
            }
            return Task.CompletedTask;
        }

        private static void DisplayStateCheck(KernelInvocationContext context, (string name, string result) item)
        {
            context.DisplayAs($"### {item.name}", "text/markdown");
            context.DisplayAs(item.result, "application/json");
        }

        public bool TryGetValue<T>(string name, out T value)
        {
            var formulaValue = _engine.GetValue(name);
            if(formulaValue != null)
            {
                value = (T)formulaValue.ToObject();
                if (value is IEnumerable<object>)
                    value = (T)(object)JsonSerializer.Serialize<dynamic>(value);
                return true;
            }

            value = default;
            return false;
        }

        Task IKernelCommandHandler<RequestValue>.HandleAsync(RequestValue command, KernelInvocationContext context)
        {
            if (TryGetValue<object>(command.Name, out var value))
            {
                context.PublishValueProduced(command, value);
            }
            else
            {
                context.Fail(command, message: $"Value '{command.Name}' not found in kernel {Name}");
            }

            return Task.CompletedTask;
        }

        async Task IKernelCommandHandler<SendValue>.HandleAsync(SendValue command, KernelInvocationContext context)
        {
            await SetValueAsync(command, context, SetAsync);

            Task SetAsync(string name, object value, Type declaredType)
            {
                switch (value)
                {
                    case string v:
                        _engine.UpdateVariable(name, name.EndsWith("json", StringComparison.InvariantCultureIgnoreCase) || (v.StartsWith("{") && v.EndsWith("}")) ? FormulaValueJSON.FromJson(v) : FormulaValue.New(v));
                        break;
                    case bool v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case decimal v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case DateTime v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case double v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case float v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case Guid v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case int v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case long v:
                        _engine.UpdateVariable(name, v);
                        break;
                    case TimeSpan v:
                        _engine.UpdateVariable(name, v);
                        break;
                    default:
                        _engine.UpdateVariable(name, PrimitiveWrapperAsUnknownObject.New(value));
                        break;
                }
                return Task.CompletedTask;
            }
        }

        Task IKernelCommandHandler<RequestCompletions>.HandleAsync(RequestCompletions requestCompletions, KernelInvocationContext context)
        {
            CompletionsProduced completion;
            var checkResult = _engine.Check(requestCompletions.Code, new ParserOptions(CultureInfo.InvariantCulture), _engine.EngineSymbols);
            var suggestions = _engine.Suggest(checkResult, requestCompletions.LinePosition.Character);
            var completionItems = suggestions.Suggestions.Select((item, index) => new CompletionItem(
                item.DisplayText.Text,
                GetCompletionItemKind(item.Kind), 
                sortText: index.ToString("D3", CultureInfo.InvariantCulture)) 
            { 
                Documentation = item.Definition
            });

            completion = new CompletionsProduced(
                completionItems,
                requestCompletions,
                SourceUtilities.GetLinePositionSpanFromStartAndEndIndices(requestCompletions.Code, suggestions.ReplacementStartIndex, suggestions.ReplacementStartIndex + suggestions.ReplacementLength));
            context.Publish(completion);
            return Task.CompletedTask;
        }

        private static string GetCompletionItemKind(SuggestionKind kind)
        {
            switch (kind)
            {
                case SuggestionKind.Function:
                case SuggestionKind.ServiceFunctionOption:
                    return "Method";
                case SuggestionKind.KeyWord:
                    return "Keyword";
                case SuggestionKind.Global:
                case SuggestionKind.Alias:
                case SuggestionKind.Local:
                case SuggestionKind.ScopeVariable:
                    return "Variable";
                case SuggestionKind.Field:
                    return "Field";
                case SuggestionKind.Enum:
                    return "Enum";
                case SuggestionKind.BinaryOperator:
                    return "Operator";
                case SuggestionKind.Service:
                    return "Module";
                default:
                    return "Text";
            }
        }
    }
}
