using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TSS.Visitors;

namespace TSS.Ast
{
    [DebuggerDisplay("{" + nameof(DisplayString) + "}")]
    public class OrSelector
    {
        public OrSelector(IEnumerable<ContainmentSelector> selectors)
        {
            ContainmentSelectors = selectors.ToList();
            if (ContainmentSelectors.Count == 0)
            {
                throw new ArgumentException("Selector must contain at least one child.");
            }
        }

        public IReadOnlyList<ContainmentSelector> ContainmentSelectors { get; }

        public bool ContainsScripts() => ContainmentSelectors.Any(c => c.ContainsScripts());

        public string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return string.Join(", ", ContainmentSelectors.Select(c => c.Serialize(scriptReplacer)));
        }

        public string DisplayString => Serialize(null);
    }

    public class ContainmentSelector
    {
        public ContainmentSelector(IEnumerable<AndSelector> selectors)
        {
            AndSelectors = selectors.ToList();
            if (AndSelectors.Count == 0)
            {
                throw new ArgumentException("Selector must contain at least one child.");
            }
        }

        public IReadOnlyList<AndSelector> AndSelectors { get; }

        public bool ContainsScripts() => AndSelectors.Any(c => c.ContainsScripts());

        public string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return string.Join(" ", AndSelectors.Select(a => a.Serialize(scriptReplacer)));
        }
    }

    public class AndSelector
    {
        public AndSelector(IEnumerable<ElementSelector> selectors)
        {
            ElementSelectors = selectors.ToList();
            if (ElementSelectors.Count == 0)
            {
                throw new ArgumentException("Selector must contain at least one child.");
            }

            TypeSelector = GetType(ElementSelectors);
            HasContextSelector = GetHasContext(ElementSelectors);
        }

        public IReadOnlyList<ElementSelector> ElementSelectors { get; }

        public string TypeSelector { get; }

        public bool HasContextSelector { get; }

        private static string GetType(IEnumerable<ElementSelector> selectors)
        {
            foreach (var selector in selectors)
            {
                if (selector is IdentifierSelector id && char.IsLetter(id.Identifier[0]))
                {
                    return id.Identifier;
                }
            }

            return null;
        }

        private static bool GetHasContext(IEnumerable<ElementSelector> selectors)
        {
            foreach (var selector in selectors)
            {
                if (selector is ContextSelector)
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsScripts() => ElementSelectors.Any(c => c.ContainsScripts());

        public string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return string.Join("^", ElementSelectors.Select(e => e.Serialize(scriptReplacer)));
        }
    }

    public abstract class ElementSelector
    {
        public abstract bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback);

        public abstract bool ContainsScripts();

        public abstract string Serialize(Func<ScriptSelector, string> scriptReplacer);
    }

    public class NotSelector : ElementSelector
    {
        public NotSelector(ElementSelector innerSelector)
        {
            InnerSelector = innerSelector;
            if (innerSelector is NotSelector || innerSelector is AnySelector || innerSelector is ContextSelector)
            {
                throw new ArgumentException("Invalid selector negation.");
            }
        }

        public ElementSelector InnerSelector { get; }

        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            return !InnerSelector.Match(current, parent, root, callback);
        }

        public override bool ContainsScripts() => InnerSelector.ContainsScripts();

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return "!" + InnerSelector.Serialize(scriptReplacer);
        }
    }

    public class AnySelector : ElementSelector
    {
        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            return true;
        }

        public override bool ContainsScripts() => false;

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return "*";
        }
    }

    public class ContextSelector : ElementSelector
    {
        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            return ReferenceEquals(current, root);
        }

        public override bool ContainsScripts() => false;

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return "&";
        }
    }

    public class ScriptSelector : ElementSelector
    {
        public ScriptSelector(string expression)
        {
            Expression = expression;
        }

        public string Expression { get; }

        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            throw new NotSupportedException();
        }

        public override bool ContainsScripts() => true;

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            if (scriptReplacer != null)
            {
                return scriptReplacer(this);
            }

            return "<?" + Expression + "?>";
        }
    }

    public delegate bool IndexedPredicate(int id, ISelectable item);

    public class CallbackSelector : ElementSelector
    {
        public CallbackSelector(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            return callback(Id, current);
        }

        public override bool ContainsScripts()
        {
            return false;
        }

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return "<{" + Id + "}>";
        }
    }

    public class IdentifierSelector : ElementSelector
    {
        public IdentifierSelector(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override bool Match(ISelectable current, ISelectable parent, ISelectable root, IndexedPredicate callback)
        {
            return current.Matches(Identifier);
        }

        public override bool ContainsScripts() => false;

        public override string Serialize(Func<ScriptSelector, string> scriptReplacer)
        {
            return "'" + Identifier.Replace("'", "''") + "'";
        }
    }
}
