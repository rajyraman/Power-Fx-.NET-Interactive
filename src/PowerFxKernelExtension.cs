using Microsoft.AspNetCore.Html;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.PowerFx;
using PowerFxDotnetInteractive;
using System.Linq;
using System.Threading.Tasks;

namespace PowerFx.Interactive
{
    public class PowerFxKernelExtension : IKernelExtension
    {
        private static RecalcEngine _engine;
        public string Name => "Power Fx";

        public async Task OnLoadAsync(Kernel kernel)
        {
            if (kernel is CompositeKernel compositeKernel)
            {
                var config = new PowerFxConfig();
                config.EnableJsonFunctions();
                config.EnableSetFunction();
                _engine = new RecalcEngine(config);
                compositeKernel.Add(new PowerFxKernel(_engine).UseValueSharing());
            }
            var supportedFunctions = new HtmlString($@"<details><summary>These are the supported Power Fx functions in {typeof(RecalcEngine).Assembly.GetName().Version}.</summary>
            <ol>{string.Join("",_engine.GetAllFunctionNames().Select(x=>$@"<li><a href=""https://docs.microsoft.com/en-us/search/?terms={x}&scope=Power%20Apps"" >{x}</a></li>"))}</ol></details><br>");

            await kernel.SendAsync(new DisplayValue(new FormattedValue(
                HtmlFormatter.MimeType,
                supportedFunctions.ToDisplayString(HtmlFormatter.MimeType))));
        }
    }
}