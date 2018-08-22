using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSS.Visitors;

namespace TSS.Ast
{
    public class Stylesheet
    {
        public Stylesheet(IEnumerable<StylesheetDeclaration> declarations)
        {
            Declarations = declarations.ToList();
        }

        public IReadOnlyList<StylesheetDeclaration> Declarations { get; }

        public bool ContainsScripts() => Declarations.Any(d => d.ContainsScripts());

        public ICompiledStylesheet CompileSimple(Func<object, ISelectable> asSelectable)
        {
            return new SimpleStylesheet(this, asSelectable);
        }

        public ICompiledStylesheet Compile(
            Func<object, ISelectable> asSelectable,
            IEvaluator evaluator,
            IDynamicInvoker invoker,
            params ScriptParameter[] closure)
        {
            if (!ContainsScripts())
            {
                return CompileSimple(asSelectable);
            }

            var compiled = CompiledScript.Compile(this, invoker, asSelectable);
            closure = compiled.Closure.Concat(closure ?? new ScriptParameter[0]).ToArray();
            var code = new StringBuilder();
            code.AppendLine($"(function ({string.Join(", ", closure.Select(c => c.Name))}) {{");
            code.AppendLine(compiled.Code);
            code.AppendLine("}).valueOf()");
            var func = invoker.Invoke(evaluator.Evaluate(code.ToString()), closure.Select(c => c.Value).ToArray());
            return new ScriptStylesheet((root, arg) => invoker.Invoke(func, root, arg));
        }

        public CompiledScript CompileJs(
            Func<object, ISelectable> asSelectable,
            IDynamicInvoker invoker)
        {
            return CompiledScript.Compile(this, invoker, asSelectable);
        }
    }

    public abstract class StylesheetStatement
    {
        public abstract bool ContainsScripts();
    }

    public class AssignmentStatement : StylesheetStatement
    {
        public AssignmentStatement(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; }

        public string Value { get; }

        public override bool ContainsScripts() => false;
    }

    public abstract class StylesheetDeclaration : StylesheetStatement
    {
    }

    public class ScriptDeclaration : StylesheetDeclaration
    {
        public ScriptDeclaration(string script)
        {
            Script = script;
        }

        public string Script { get; }

        public override bool ContainsScripts() => true;
    }

    public class StyleDeclaration : StylesheetDeclaration
    {
        public StyleDeclaration(OrSelector selector, IEnumerable<StylesheetStatement> statements)
        {
            Selector = selector;
            Statements = statements.ToList();
        }

        public OrSelector Selector { get; }

        public IReadOnlyList<StylesheetStatement> Statements { get; }

        public override bool ContainsScripts()
        {
            return Selector.ContainsScripts() || Statements.Any(s => s.ContainsScripts());
        }
    }
}