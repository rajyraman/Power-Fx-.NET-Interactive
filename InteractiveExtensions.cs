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
        public static string FormatOutput(this string s) => $"<code>{s.ReplaceNewLines()}</code>";
        public static string FormatInput(this string s) => $@"<pre style=""font-family: Consolas"" >{s.ReplaceNewLines()}</pre>";

        public static string ReplaceNewLines(this string s)
        {
            s = s.Replace(" ", "&nbsp;");
            var o = string.Join("<br>", s.Split("\r\n"));
            return string.Join("<br>", o.Split("\n"));
        }

        public static string FormatJson(this string s)
        {
            return JsonSerializer.Serialize(JsonSerializer.Deserialize<dynamic>(s), 
                new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
