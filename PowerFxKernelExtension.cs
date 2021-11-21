// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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

namespace PowerFx.Interactive
{
    public class PowerFxKernelExtension :  IKernelExtension, IStaticContentSource
    {
        private static RecalcEngine _engine;
        public string Name => "Power Fx";

        public async Task OnLoadAsync(Kernel kernel)
        {
            if (kernel is CompositeKernel compositeKernel)
            {
#if DEBUG
                Debugger.Launch();
#endif
                _engine = new RecalcEngine();
                compositeKernel.Add(new PowerFxKernel(_engine));
            }
            var message = new HtmlString($@"<details><summary>Evaluate <a href=""https://github.com/microsoft/Power-Fx"">PowerFx Expressions</a>.</summary>");


            var formattedValue = new FormattedValue(
                HtmlFormatter.MimeType,
                message.ToDisplayString(HtmlFormatter.MimeType));

            await kernel.SendAsync(new DisplayValue(formattedValue));
        }
    }
}