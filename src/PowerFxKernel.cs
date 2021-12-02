using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.DotNet.Interactive.ValueSharing;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    public class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, ISupportSetClrValue, ISupportGetValue
    {
        private static RecalcEngine _engine;
        private List<string> _identifiers;

        public PowerFxKernel(RecalcEngine engine) : base("powerfx")
        {
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

        public Task SetValueAsync(string name, object value, Type declaredType = null)
        {
            switch (value)
            {
                case string v:
                    _engine.UpdateVariable(name, name.EndsWith("json", StringComparison.InvariantCultureIgnoreCase) || (v.StartsWith("{") && v.EndsWith("}")) ? FormulaValue.FromJson(v) : FormulaValue.New(v));
                    break;
                case IEnumerable<object> t:
                    _engine.UpdateVariable(name, FormulaValue.NewTable(t));
                    break;
                default:
                    _engine.UpdateVariable(name, FormulaValue.New(value, value.GetType()));
                    break;
            }
            return Task.CompletedTask;
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

        public IReadOnlyCollection<KernelValueInfo> GetValueInfos()
        {
            if (_identifiers.Any()) {
                return new ReadOnlyCollection<KernelValueInfo>(_identifiers.Select(x => new KernelValueInfo(x, _engine.GetValue(x).GetType())).ToList()) ;
            }

            return Array.Empty<KernelValueInfo>();
        }
    }
}
