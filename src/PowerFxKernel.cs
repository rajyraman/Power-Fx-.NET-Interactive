using Azure.Identity;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Directives;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Dataverse;
using Microsoft.PowerFx.Intellisense;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    public partial class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, IKernelCommandHandler<RequestValue>, IKernelCommandHandler<SendValue>, IKernelCommandHandler<RequestCompletions>
    {
        private static RecalcEngine _engine;
        private static ObjectCache _cache;
        private static ReadOnlySymbolTable _symbolTable;
        private static TypeMarshallerCache _typeMarshallerCache = new();

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
            var connectionString = new KernelDirectiveParameter("--connectionString", "Connection string to connect to the Dataverse environment");
            var environment = new KernelDirectiveParameter("--environment", "Environment URL of the Dataverse environment");
            var runPowerFxDataverseCommand = new KernelActionDirective("#!dataverse-powerfx") { Description = "Run a Power Fx query on the Dataverse environment", Parameters = [connectionString, environment] };
            Root.AddDirective(runPowerFxDataverseCommand, (_, _) => Task.CompletedTask);
            //var runPowerFxDataverseCommand = new Command("#!dataverse-powerfx", "Run a Power Fx query on the Dataverse environment") { connectionString, environment };
            //Root.AddDirective(runPowerFxDataverseCommand);
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
                var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole());
                var logger = loggerFactory.CreateLogger("dataverse");
                var client = AzAuth.CreateServiceClient(new ConnectionOptions
                {
                    ServiceUri = new Uri(environmentUrl),
                    RequireNewInstance = true,
                    Logger = logger
                },
                credentialOptions: new DefaultAzureCredentialOptions
                {
                    ExcludeEnvironmentCredential = true,
                    ExcludeManagedIdentityCredential = true,
                    ExcludeSharedTokenCacheCredential = true,
                    ExcludeWorkloadIdentityCredential = true,
                });
                var dataverseConnection = SingleOrgPolicy.New(client);
                _symbolTable = ReadOnlySymbolTable.Compose(_engine.EngineSymbols, dataverseConnection.Symbols);
                var powerFxResult = new PowerFxExpression(_engine, submitCode.Code).Evaluate(dataverseConnection.SymbolValues);
                PrintResult(context, powerFxResult);
            }
            else
            {
                var powerFxResult = new PowerFxExpression(_engine, submitCode.Code).Evaluate();
                PrintResult(context, powerFxResult);
                _identifiers = PowerFxExpression.Identifiers;
            }
            return Task.CompletedTask;
        }

        private static void PrintResult(KernelInvocationContext context, List<EvalResult> powerFxResult)
        {
            powerFxResult.ForEach(x =>
            {
                if (string.IsNullOrEmpty(x.MimeType))
                    context.DisplayAs("Empty result..", "text/plain");
                else
                    context.DisplayAs(x.Result, x.MimeType);
            });
        }

        private static (string connectionStringValue, string environmentUrl) GetEnvironmentUrl(SubmitCode submitCode, SubmitCode parentCode)
        {
            var originalCode = parentCode.Code.Replace(submitCode.Code, "").Replace(@"""", "");
            var connectionStringIndex = originalCode.IndexOf("--connectionString");
            var connectionStringValue = "";
            string environmentUrl;
            if (connectionStringIndex != -1)
            {
                connectionStringValue = originalCode[connectionStringIndex..].Replace(@"--connectionString", "").Trim();
                environmentUrl = UrlRegex().Matches(connectionStringValue).First().Value;
            }
            else
            {
                environmentUrl = originalCode[originalCode.IndexOf("--environment")..].Replace(@"--environment", "").Trim();
            }
            return (connectionStringValue, environmentUrl);
        }

        Task IKernelCommandHandler<RequestValue>.HandleAsync(RequestValue command, KernelInvocationContext context)
        {
            var value = _engine.GetValue(command.Name);
            if (value != null)
            {
                context.PublishValueProduced(command, value.ToObject());
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
                var marshalledFV = _typeMarshallerCache.Marshal(value, value.GetType());
                var result = _engine.Eval($"{marshalledFV.ToExpression()}");
                _engine.UpdateVariable(name, result);
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
            }).ToList();

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