using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace TSS.Visitors
{
    [DebuggerDisplay("{" + nameof(ClassString) + "}")]
    public sealed class StyleContainer :
        Collection<StyleContainer>,
        IStyleable,
        IScriptSelectable,
        ISelectableTree,
        IScriptWritable
    {
        public StyleContainer(string classes)
        {
            Classes = new HashSet<string>((classes ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Values = new Dictionary<string, string>();
            Code = new List<string>();
            CodeSelectors = new List<string>();
        }

        public string ClassString => string.Join(" ", Classes);

        public HashSet<string> Classes { get; }

        public Dictionary<string, string> Values { get; }

        public List<string> Code { get; }

        public List<string> CodeSelectors { get; }

        public bool Matches(string condition) => Classes.Contains(condition);

        public object Get(string key)
        {
            Values.TryGetValue(key, out var result);
            return result;
        }

        public void Set(string key, object value) => Values[key] = value?.ToString() ?? "[null]";

        public IEnumerable<ISelectable> GetChildren() => this;

        public IScriptReadable ScriptAccessor => this;

        public override string ToString() => ToString("");

        private string ToString(string indent)
        {
            return $"{indent}{string.Join(" ", Classes)} {{{string.Join("", Values.Select(kvp => "\n  " + indent + kvp.Key + ": " + kvp.Value + ";"))}{(Values.Count > 0 ? "\n" + indent : " ")}}}"
                + (Count != 0 ? $"\n{indent}{{{string.Join("", this.Select(c => "\n" + c.ToString(indent + "  ")))}\n{indent}}}" : "");
        }
    }
}
