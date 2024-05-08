using Azure.Core;
using Azure.Identity;
using Dumpify;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Utility;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Dataverse;
using Microsoft.PowerFx.Intellisense;
using Microsoft.PowerFx.Types;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    public partial class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, IKernelCommandHandler<RequestValue>, IKernelCommandHandler<SendValue>, IKernelCommandHandler<RequestCompletions>
    {
        private static RecalcEngine _engine;
        private static ObjectCache _cache;
        private static ReadOnlySymbolTable _symbolTable;
        private static Dictionary<string,(ServiceClient serviceClient, DataverseConnection dataverseConnection)> _connections = new();

        private List<string> _identifiers;

        public PowerFxKernel(RecalcEngine engine) : base("powerfx")
        {
#if DEBUG
            if (!Debugger.IsAttached) Debugger.Launch();
#endif
            _cache = MemoryCache.Default;
            KernelInfo.LanguageName = "Power Fx";
            KernelInfo.Description = "This Kernel can evaluate Power Fx snippets.";
            KernelInfo.LanguageVersion = typeof(RecalcEngine).Assembly.GetName().Version.ToString();
            var connectionString = new Option<string>("--connectionString", "Connection string to connect to the Dataverse environment");
            connectionString.AddAlias("-c");
            var environment = new Option<string>("--environment", "Environment URL of the Dataverse environment");
            environment.AddAlias("-e");
            var runPowerFxDataverseCommand = new Command("#!dataverse-powerfx", "Run a Power Fx query on the Dataverse environment") { connectionString, environment };
            Root.AddDirective(runPowerFxDataverseCommand);
            _engine = engine;
        }
        public static RecalcEngine GetRecalcEngine() => _engine;

        public Task HandleAsync(SubmitCode submitCode, KernelInvocationContext context)
        {
            SubmitCode parentCode = submitCode.Parent as SubmitCode;
            _symbolTable = _engine.EngineSymbols;

            if (parentCode != null && parentCode.Code.StartsWith("#!dataverse-powerfx"))
            {
                var (connectionStringValue, environmentUrl) = GetEnvironmentUrl(submitCode, parentCode);
                if (!_connections.TryGetValue(environmentUrl, out (ServiceClient serviceClient, DataverseConnection dataverseConnection) currentConnection))
                {
                    var client = !string.IsNullOrEmpty(connectionStringValue) ? new ServiceClient(connectionStringValue) : new ServiceClient(
                                    tokenProviderFunction: async f => await GetToken(environmentUrl, 
                                    new ChainedTokenCredential(
                                        new AzureCliCredential(), 
                                        new VisualStudioCodeCredential(), 
                                        new VisualStudioCredential(), 
                                        new AzurePowerShellCredential(), 
                                        new InteractiveBrowserCredential()), 
                                    _cache),
                                    useUniqueInstance: true,
                                    instanceUrl: new Uri(environmentUrl));
                    var dataverseConnection = SingleOrgPolicy.New(client);
                    _engine.EnableDelegation(1000);
                    currentConnection = (client, dataverseConnection);
                    _connections.Add(environmentUrl, currentConnection);
                }
                _symbolTable = ReadOnlySymbolTable.Compose(_engine.EngineSymbols, currentConnection.dataverseConnection.Symbols);
                var powerFxResult = new PowerFxExpression(_engine, submitCode.Code, context).Evaluate(currentConnection.dataverseConnection.SymbolValues);
                //var lines = submitCode.Code.SplitLines();
                //foreach (var line in lines)
                //{
                //    var result = _engine.EvalAsync(line, default, currentConnection.dataverseConnection.SymbolValues).Result;
                //    var entityObject = result.ToObject();
                //    switch (entityObject)
                //    {
                //        case IEnumerable<object> e:
                //            if (!e.Any()) break;

                //            if (e.First() is ExpandoObject)
                //            {
                //                context.Display(e, "application/json");
                //            }
                //            else
                //            {
                //                var o = e.DumpText();
                //                context.DisplayAs(o, "text/plain");
                //            }
                //            break;
                //        default:
                //            entityObject.Dump();
                //            break;
                //    }
                //}
            }
            else
            {
                var powerFxResult = new PowerFxExpression(_engine, submitCode.Code, context).Evaluate();
                _identifiers = PowerFxExpression.Identifiers;
            }
            return Task.CompletedTask;
        }
        private async Task<string> GetToken(string environment, ChainedTokenCredential credential, ObjectCache cache)
        {
            //TokenProviderFunction is called multiple times, so we need to check if we already have a token in the cache
            var accessToken = cache.Get(environment);
            if (accessToken == null)
            {
                accessToken = (await credential.GetTokenAsync(new TokenRequestContext(new[] { $"{environment}/.default" })));
                cache.Set(environment, accessToken, new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(50) });
            }
            return ((AccessToken)accessToken).Token;
        }

        private static (string connectionStringValue, string environmentUrl) GetEnvironmentUrl(SubmitCode submitCode, SubmitCode parentCode)
        {
            var originalCode = parentCode.Code.Replace(submitCode.Code, "").Replace(@"""", "");
            var connectionStringIndex = originalCode.IndexOf("-c");
            var connectionStringValue = "";
            string environmentUrl;
            if (connectionStringIndex != -1)
            {
                connectionStringValue = originalCode[connectionStringIndex..].Replace(@"-c", "").Trim();
                environmentUrl = UrlRegex().Matches(connectionStringValue).First().Value;
            }
            else
            {
                environmentUrl = originalCode[originalCode.IndexOf("-e")..].Replace(@"-e", "").Trim();
            }
            return (connectionStringValue, environmentUrl);
        }

        private static void DisplayStateCheck(KernelInvocationContext context, (string name, string result) item)
        {
            context.DisplayAs($"### {item.name}", "text/markdown");
            context.DisplayAs(item.result, "application/json");
        }

        public static bool TryGetValue<T>(string name, out T value)
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

            var checkResult = _engine.Check(requestCompletions.Code, new ParserOptions(CultureInfo.InvariantCulture), _symbolTable);
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
