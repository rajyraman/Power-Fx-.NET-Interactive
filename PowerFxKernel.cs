using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.DotNet.Interactive.ValueSharing;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>, ISupportSetClrValue, ISupportGetValue
    {
        private readonly RecalcEngine _engine;
        public PowerFxKernel(RecalcEngine engine) : base("PowerFx")
        {
            _engine = engine;
        }
        public Task HandleAsync(SubmitCode code, KernelInvocationContext context)
        {
            var powerFxResult = new PowerFxExpression(_engine, code.Code).Evaluate();
            var result = powerFxResult.Result.Select(x => $"<tr><td>{x.formula.FormatInput()}</td><td>{x.result.FormatOutput()}</td>");
            var stateCheckResult = powerFxResult.StateCheck.Select(x => $"<tr><td>{x.name.FormatInput()}</td><td>{x.result.FormatOutput()}</td></tr>");
            var stateCheckMarkdown = $"<tr><th>Variable</th><th>Result</th></tr>{string.Join("\n", stateCheckResult)}";
            if (code.Code == "?")
            {
                context.DisplayAs(stateCheckMarkdown.Table(), "text/markdown");
            }
            else
            {
                var evaluationMarkdown = $"<tr><th>Expression</th><th>Result</th></tr>{"\n"}{string.Join("\n", result)}";
                context.DisplayAs(evaluationMarkdown.Table(), "text/markdown");
                if (powerFxResult.Result.Count == 1 && powerFxResult.StateCheck.Any(x => x.name == powerFxResult.Result.First().variable))
                {
                    var item = powerFxResult.StateCheck.FirstOrDefault(x => x.name == powerFxResult.Result.First().variable);
                    DisplayStateCheck(context, item);
                }
                else
                {
                    foreach (var item in powerFxResult.StateCheck)
                    {
                        DisplayStateCheck(context, item);
                    }
                }
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
                case DateTime v:
                    _engine.UpdateVariable(name, FormulaValue.New(v));
                    break;
                case bool v:
                    _engine.UpdateVariable(name, FormulaValue.New(v));
                    break;
                case double v:
                    _engine.UpdateVariable(name, FormulaValue.New(v));
                    break;
                case float v:
                    _engine.UpdateVariable(name, FormulaValue.New(v));
                    break;
                case int v:
                    _engine.UpdateVariable(name, FormulaValue.New(v));
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
                return true;
            }

            value = default;
            return false;
        }

        public IReadOnlyCollection<KernelValueInfo> GetValueInfos()
        {
            //TODO: Get all values from engine

            return Array.Empty<KernelValueInfo>();
        }
    }
}
