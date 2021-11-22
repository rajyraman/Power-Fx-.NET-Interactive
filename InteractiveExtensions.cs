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
        public static string FormatOutput(this string s) => $"<pre><code>{s.ReplaceNewLines()}</code></pre>";
        public static string FormatInput(this string s) => $@"<pre style=""font-family: Consolas"" >{s.ReplaceNewLines()}</pre>";

        public static string ReplaceNewLines(this string s)
        {
            s = s.Replace(" ", "&nbsp;");
            var o = string.Join("<br>", s.Split("\r\n"));
            return string.Join("<br>", o.Split("\n"));
        }

        public static string FormatJson(this string s) => JsonSerializer.Serialize(JsonSerializer.Deserialize<dynamic>(s), new JsonSerializerOptions { WriteIndented = true });

        public static string Table(this string s) => $"<table><style>table {{ width: 95vw; }} th:first-child,td:first-child {{overflow: auto; max-width: 30vw;}} th:last-child,td:last-child {{overflow: auto; min-width: 60vw;}}</style>{s}</table>";
    }
}
