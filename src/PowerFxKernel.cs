using Dumpify;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.DotNet.Interactive.Utility;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Dataverse;
using Microsoft.PowerFx.Intellisense;
using Microsoft.PowerFx.Types;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    public partial class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, IKernelCommandHandler<RequestValue>, IKernelCommandHandler<SendValue>, IKernelCommandHandler<RequestCompletions>
    {
        private static RecalcEngine _engine;
        private static Dictionary<string,ServiceClient> _connections = new();
        private static DataverseConnection _dataverseConnection;

        private List<string> _identifiers;

        public PowerFxKernel(RecalcEngine engine) : base("powerfx")
        {
#if DEBUG
            if (!Debugger.IsAttached) Debugger.Launch();
#endif
            ObjectCache cache = MemoryCache.Default;
            KernelInfo.LanguageName = "powerfx";
            KernelInfo.Description = "This Kernel can evaluate Power Fx snippets.";
            KernelInfo.LanguageVersion = typeof(RecalcEngine).Assembly.GetName().Version.ToString();
            var connectionString = new Option<string>("--connectionString", "Connection string for the Dataverse environment") { IsRequired = true};
            connectionString.AddAlias("-c");
            var runPowerFxDataverseCommand = new Command("#!dataverse-powerfx", "Run a Power Fx query on the Dataverse environment") { connectionString };
            Root.AddDirective(runPowerFxDataverseCommand);
            _engine = engine;
        }

        public static RecalcEngine GetRecalcEngine() => _engine;

        public Task HandleAsync(SubmitCode submitCode, KernelInvocationContext context)
        {
            SubmitCode parentCode = submitCode.Parent as SubmitCode;
            if (parentCode != null && parentCode.Code.StartsWith("#!dataverse-powerfx"))
            {
                var originalCode = parentCode.Code.Replace(submitCode.Code, "").Replace(@"""", "");
                var connectionStringIndex = originalCode.IndexOf("-c");
                var connectionStringValue = originalCode[connectionStringIndex..].Replace(@"-c", "").Trim();
                var environmentUrl = UrlRegex().Matches(connectionStringValue).First().Value;
                if (!_connections.ContainsKey(environmentUrl))
                {
                    var client = new ServiceClient(connectionStringValue);
                    _connections.Add(environmentUrl, client);
                    _dataverseConnection = SingleOrgPolicy.New(client);
                    _engine.EnableDelegation();
                }
                var result = _engine.EvalAsync(submitCode.Code, default, _dataverseConnection.SymbolValues).Result;
                var entityObject = result.ToObject();
                var entityText = entityObject.DumpText();
                context.DisplayAs(entityText, "text/plain");
            }
            else
            {
                var powerFxResult = new PowerFxExpression(_engine, submitCode.Code).Evaluate();
                _identifiers = powerFxResult.Identifiers;

                var result = powerFxResult.Result.Select(x => $"<tr><td>{x.formula.FormatInput()}</td><td>{x.result.FormatOutput()}</td>");
                var stateCheckResult = powerFxResult.StateCheck.Select(x => $"<tr><td>{x.name.FormatInput()}</td><td>{x.result.FormatOutput()}</td></tr>");
                var stateCheckMarkdown = $"<tr><th>Variable</th><th>Result</th></tr>{string.Join("\n", stateCheckResult)}";
                if (submitCode.Code == "?")
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

            static Task SetAsync(string name, object value, Type declaredType)
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
            return kind switch
            {
                SuggestionKind.Function or SuggestionKind.ServiceFunctionOption => "Method",
                SuggestionKind.KeyWord => "Keyword",
                SuggestionKind.Global or SuggestionKind.Alias or SuggestionKind.Local or SuggestionKind.ScopeVariable => "Variable",
                SuggestionKind.Field => "Field",
                SuggestionKind.Enum => "Enum",
                SuggestionKind.BinaryOperator => "Operator",
                SuggestionKind.Service => "Module",
                _ => "Text",
            };
        }

        [GeneratedRegex(@"((http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-.]*[\w]))")]
        private static partial Regex UrlRegex();
    }
}
