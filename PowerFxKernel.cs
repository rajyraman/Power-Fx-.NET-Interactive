using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.PowerFx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerFxDotnetInteractive
{
    class PowerFxKernel : Kernel, IKernelCommandHandler<SubmitCode>
    {
        private readonly RecalcEngine _engine;
        private const string tableTemplate = @"<table><style>table { width: 95vw; }</style>|header|body|</table>";
        public PowerFxKernel(RecalcEngine engine) : base("PowerFx")
        {
            _engine = engine;
        }
        public Task HandleAsync(SubmitCode command, KernelInvocationContext context)
        {
            var powerFxResult = new PowerFxExpression(_engine, command.Code).Evaluate();
            var result = powerFxResult.Result.Select(x => $"<tr><td>{x.formula.FormatInput()}</td><td>{x.result.FormatOutput()}</td>");
            var stateCheckResult = powerFxResult.StateCheck.Select(x => $"<tr><td>{x.name.FormatInput()}</td><td>{x.result.FormatOutput()}</td></tr>");
            var stateCheckMarkdown = $"<tr><th>Variable</th><th>Result</th></tr>{string.Join("\n", stateCheckResult)}";
            if (command.Code == "?")
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

    }
}
