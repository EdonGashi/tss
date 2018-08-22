using System.Collections.Generic;

namespace TSS.Visitors
{
    public interface ISelectable
    {
        bool Matches(string condition);
    }

    public interface ISelectableTree : ISelectable
    {
        IEnumerable<ISelectable> GetChildren();
    }

    public interface IPruneableSelectableTree : ISelectableTree
    {
        bool IsUniqueInPath();

        bool CanContainDescendant(string type);
    }

    public interface IStyleable : ISelectable
    {
        void Set(string key, object value);
    }

    public interface IScriptSelectable : ISelectable
    {
        IScriptReadable ScriptAccessor { get; }
    }

    public interface IScriptReadable
    {
        object Get(string key);
    }

    public interface IScriptWritable : IScriptReadable
    {
        void Set(string key, object value);
    }

    public interface IScriptDeletable : IScriptWritable
    {
        void Delete(string key);
    }

    public interface IEvaluator
    {
        object Evaluate(string code);
    }

    public interface IDynamicInvoker
    {
        object Invoke(object func, params object[] args);
    }

    public class ScriptParameter
    {
        public ScriptParameter(string name, object value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public object Value { get; }
    }
}
