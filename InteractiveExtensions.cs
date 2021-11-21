using Microsoft.AspNetCore.Html;
using Microsoft.DotNet.Interactive;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Values;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Microsoft.DotNet.Interactive.Formatting.PocketViewTags;

namespace PowerFxDotnetInteractive
{
    public static class InteractiveExtensions
    {
        public static string FormatOutput(this string s) => $"`{s.ReplaceNewLines()}`";
        public static string FormatInput(this string s) => $@"<pre style=""font-family: Consolas"" >{s.ReplaceNewLines()}</pre>";

        public static string ReplaceNewLines(this string s)
        {
            var o = string.Join("<br>", s.Split("\r\n"));
            return string.Join("<br>", o.Split("\n"));
        }
    }
}
