using System.Collections.Generic;
using TSS.Ast;
using TSS.Parsing;

namespace TSS.Visitors
{
    public static class SelectorHelper
    {
        private static readonly Dictionary<string, OrSelector> Cache = new Dictionary<string, OrSelector>();

        public delegate void VisitorCallback(
            ISelectable current,
            ISelectableTree parent,
            ISelectable root);

        private class SelectorTracker
        {
            private readonly OrSelector selector;
            private readonly int[] positions;

            private SelectorTracker(OrSelector selector, int[] positions)
            {
                this.selector = selector;
                this.positions = positions;
            }

            public SelectorTracker(OrSelector selector)
            {
                this.selector = selector;
                positions = new int[selector.ContainmentSelectors.Count];
            }

            public SelectorTracker Clone()
            {
                return new SelectorTracker(selector, (int[])positions.Clone());
            }

            public int Lines => positions.Length;

            public void Prune(int line) => positions[line] = -1;

            public bool IsPruned(int line) => positions[line] < 0;

            public (bool isTerminal, AndSelector selector) LineState(int line)
            {
                var selectors = selector.ContainmentSelectors[line].AndSelectors;
                var position = positions[line];
                if (position < 0)
                {
                    return (true, null);
                }

                if (position >= selectors.Count - 1)
                {
                    return (true, selectors[selectors.Count - 1]);
                }

                return (false, selectors[position]);
            }

            public IEnumerable<AndSelector> RemainingRules(int line)
            {
                var selectors = selector.ContainmentSelectors[line].AndSelectors;
                var position = positions[line];
                if (position < 0)
                {
                    yield break;
                }

                if (position >= selectors.Count - 1)
                {
                    yield return selectors[selectors.Count - 1];
                }
                else
                {
                    for (var i = position; i < selectors.Count; i++)
                    {
                        yield return selectors[i];
                    }
                }
            }

            public void Advance(int line)
            {
                var selectors = selector.ContainmentSelectors[line].AndSelectors;
                var position = positions[line];
                if (position >= 0 && position < selectors.Count)
                {
                    positions[line]++;
                }
            }
        }

        public static void Visit(
            ISelectable root,
            string selector,
            VisitorCallback visitor)
        {
            Visit(root, selector, visitor, null);
        }

        public static void Visit(
            ISelectable root,
            string selector,
            VisitorCallback visitor,
            IndexedPredicate predicate)
        {
            if (Cache.TryGetValue(selector, out var or))
            {
                Visit(root, or, visitor, predicate);
                return;
            }

            or = Parser.ParseOrSelector(new TokenStream(Token.Parse(selector)), false);
            Cache[selector] = or;
            Visit(root, or, visitor, predicate);
        }

        public static void Visit(
            ISelectable root,
            OrSelector selector,
            VisitorCallback visitor)
        {
            Visit(root, selector, visitor, null);
        }

        public static void Visit(
            ISelectable root,
            OrSelector selector,
            VisitorCallback visitor,
            IndexedPredicate predicate)
        {
            Visit(
                root,
                null,
                root,
                new SelectorTracker(selector),
                visitor,
                predicate);
        }

        private static void Visit(
            ISelectable current,
            ISelectableTree parent,
            ISelectable root,
            SelectorTracker tracker,
            VisitorCallback visitor,
            IndexedPredicate predicate)
        {
            var lines = tracker.Lines;
            var linesAlive = 0;
            var visit = false;
            var tree = current as ISelectableTree;
            var pruneable = current as IPruneableSelectableTree;
            for (var line = 0; line < lines; line++)
            {
                if (tracker.IsPruned(line))
                {
                    continue;
                }

                var (terminal, rule) = tracker.LineState(line);
                var isMatch = true;
                foreach (var s in rule.ElementSelectors)
                {
                    if (!s.Match(current, parent, root, predicate))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    if (terminal)
                    {
                        visit = true;
                    }
                    else
                    {
                        tracker.Advance(line);
                    }
                }

                if ((!isMatch || terminal) && rule.HasContextSelector)
                {
                    tracker.Prune(line);
                    continue;
                }

                if (pruneable != null)
                {
                    if ((!isMatch || terminal) && pruneable.IsUniqueInPath())
                    {
                        var type = rule.TypeSelector;
                        if (type != null && pruneable.Matches(type))
                        {
                            tracker.Prune(line);
                            continue;
                        }
                    }

                    var pruned = false;
                    // ReSharper disable once LoopCanBeConvertedToQuery
                    foreach (var selector in tracker.RemainingRules(line))
                    {
                        if (selector.HasContextSelector)
                        {
                            tracker.Prune(line);
                            pruned = true;
                            break;
                        }

                        var type = selector.TypeSelector;
                        if (type != null && !pruneable.CanContainDescendant(type))
                        {
                            tracker.Prune(line);
                            pruned = true;
                            break;
                        }
                    }

                    if (!pruned)
                    {
                        linesAlive++;
                    }
                }
                else
                {
                    linesAlive++;
                }
            }

            if (visit)
            {
                visitor(current, parent, root);
            }

            if (linesAlive > 0 && tree != null)
            {
                foreach (var child in tree.GetChildren())
                {
                    Visit(child, tree, root, tracker.Clone(), visitor, predicate);
                }
            }
        }
    }
}
