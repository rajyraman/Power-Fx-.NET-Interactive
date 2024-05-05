// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.Formatting;
using Microsoft.PowerFx;
using PowerFxDotnetInteractive;
using static Microsoft.DotNet.Interactive.Formatting.PocketViewTags;
using Microsoft.DotNet.Interactive.Formatting.Csv;
using Microsoft.DotNet.Interactive.ValueSharing;
using System.Collections.Generic;

namespace PowerFx.Interactive
{
    public class PowerFxKernelExtension : IKernelExtension
    {
        private static RecalcEngine _engine;
        private static Kernel _kernel;
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