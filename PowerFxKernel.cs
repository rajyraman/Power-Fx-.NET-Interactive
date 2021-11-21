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
        public PowerFxKernel(RecalcEngine engine) : base("PowerFx")
        {
            _engine = engine;
        }
        public Task HandleAsync(SubmitCode command, KernelInvocationContext context)
        {
            var powerFxResult = new PowerFxExpression(_engine, command.Code).Evaluate();
            //context.Display(powerFxResult.Result);
            var output = $"| Expression | Result |{"\n"}| - | - |{"\n"}{string.Join("\n", powerFxResult.Result.Select(x => $"| {x.formula.FormatInput()} | {x.result.FormatOutput()} |"))}";
            //var x = powerFxResult.Result.Last();
            //var output = $"`{x.result}`";
            context.DisplayAs(output, "text/markdown");
            return Task.CompletedTask;
        }
    }
}
