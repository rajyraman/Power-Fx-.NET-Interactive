using Dumpify;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Types;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PowerFxDotnetInteractive
{
    public record EvalResult (string Result, string MimeType);

    public class PowerFxExpression(RecalcEngine engine, string expression)
    {
        private readonly string[] _assignments = ["Set", "Collect", "ClearCollect"];
        public static List<string> _identifiers = [];
        public static List<string> Identifiers => _identifiers;

        EvalResult EvaluateSingleExpression(string originalExpression, string expression, ReadOnlySymbolValues symbolValues = null)
        {
            EvalResult output = new EvalResult("","");
            Match match = _assignments.Select(assignmentFunction => Regex.Match(expression, @$"^\s*{assignmentFunction}\(\s*(?<ident>\w+)\s*,\s*(?<expr>.*)\)\s*$")).FirstOrDefault(x => x.Success);

            // variable assignment: Set or Collect or ClearCollect( <ident>, <expr> )
            if (match != null)
            {
                var result = symbolValues == null ? engine.Eval(match.Groups["expr"].Value) : engine.EvalAsync(match.Groups["expr"].Value, default, symbolValues).Result;
                engine.UpdateVariable(match.Groups["ident"].Value, result);
                if (!_identifiers.Contains(match.Groups["ident"].Value))
                    _identifiers.Add(match.Groups["ident"].Value);
                output = FormatResult(result);
            }

            // formula definition: <ident> = <formula>
            else if ((match = Regex.Match(expression, @"^\s*(?<ident>\w+)\s*=(?<formula>.*)$")).Success)
            {
                engine.SetFormula(match.Groups["ident"].Value, match.Groups["formula"].Value, (string name, FormulaValue result)=> 
                {
                    output = FormatResult(result);
                });
                if (!_identifiers.Contains(match.Groups["ident"].Value))
                    _identifiers.Add(match.Groups["ident"].Value);
            }

            // eval and print everything else, unless empty lines and single line comment (which do nothing)
            else if (!Regex.IsMatch(expression, @"^\s*//") && Regex.IsMatch(expression, @"\w"))
            {
                var result = symbolValues == null ? engine.Eval(expression) : engine.EvalAsync(expression, default, symbolValues).Result;
                output = FormatResult(result);
            }
            return output;
        }

        private static EvalResult FormatResult(FormulaValue result)
        {
            var entityObject = result.ToObject();
            switch (entityObject)
            {
                case IEnumerable<object> e:
                    if (!e.Any()) return new EvalResult("", "");
                    return e.First() is ExpandoObject ? new EvalResult(JsonSerializer.Serialize(e, new JsonSerializerOptions { WriteIndented = true}), "application/json") : new EvalResult(e.DumpText(), "text/plain");
                case ExpandoObject o:
                    return new EvalResult(JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }), "application/json");
                default:
                    return new EvalResult(entityObject.DumpText(), "text/plain");
            }
        }

        public List<EvalResult> Evaluate(ReadOnlySymbolValues symbolValues = null)
        {
            DumpConfig.Default.TypeNamingConfig.UseAliases = true;
            DumpConfig.Default.TypeNamingConfig.ShowTypeNames = false;
            DumpConfig.Default.TableConfig.ShowArrayIndices = false;

            string[] expressions = null;
            if (expression.Contains(';'))
            {
                expressions = expression.Split($";\n");
            }
            else
                expressions = expression.Split("\n");
            if (expressions.Length == 1)
                expressions = expression.Split($";\r\n");
            return expressions.Select(expression =>
            {
                var cleanedExpression = string.Join("", expression.Trim().Split("\n")).TrimEnd(';');
                return EvaluateSingleExpression(expression, cleanedExpression, symbolValues);
            }).ToList();
        }
    }
}