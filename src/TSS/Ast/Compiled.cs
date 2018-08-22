using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TSS.Visitors;

namespace TSS.Ast
{
    public class CompiledScript
    {
        public class InjectedServices
        {
            private static readonly object[] Empty = new object[0];

            private readonly Stylesheet stylesheet;
            private readonly IDynamicInvoker dynamicInvoker;
            private readonly Func<object, ISelectable> asSelectable;

            public InjectedServices(
                Stylesheet stylesheet,
                IDynamicInvoker dynamicInvoker,
                Func<object, ISelectable> asSelectable)
            {
                this.stylesheet = stylesheet;
                this.dynamicInvoker = dynamicInvoker;
                this.asSelectable = asSelectable;
            }

            public object AsScriptable(object item)
            {
                if (item is IScriptSelectable scriptSelectable)
                {
                    return scriptSelectable.ScriptAccessor;
                }

                var resolved = asSelectable(item);
                if (resolved is IScriptSelectable scriptSelectable2)
                {
                    return scriptSelectable2.ScriptAccessor;
                }

                return item;
            }

            public void Assign(object root, string key, string value)
            {
                if (asSelectable(root) is IStyleable styleable)
                {
                    styleable.Set(key, value);
                }
            }

            public int GetScriptPrivilege(object item)
            {
                switch (item)
                {
                    case IScriptDeletable _:
                        return 3;
                    case IScriptWritable _:
                        return 2;
                    case IScriptReadable _:
                        return 1;
                    default:
                        return 0;
                }
            }

            public void VisitScript(object root, string selector, object callback, params object[] predicates)
            {
                if (predicates == null)
                {
                    predicates = Empty;
                }

                var selectable = asSelectable(root);
                if (selectable != null)
                {
                    SelectorHelper.Visit(
                        selectable,
                        selector,
                        (current, parent, root1) => dynamicInvoker.Invoke(callback, current),
                        (i, s) => i >= 0 && i < predicates.Length && dynamicInvoker.Invoke(predicates[i], s) is true);
                }
            }

            public void VisitRule(object root, string id)
            {
                var parts = id.Split('.').Select(int.Parse).ToArray();
                StylesheetStatement current = stylesheet.Declarations[parts[0]];
                for (var i = 1; i < parts.Length; i++)
                {
                    current = ((StyleDeclaration)current).Statements[parts[i]];
                }

                SimpleStylesheet.Accept(asSelectable(root), current);
            }
        }

        internal static CompiledScript Compile(
            Stylesheet stylesheet,
            IDynamicInvoker dynamicInvoker,
            Func<object, ISelectable> asSelectable)
        {
            var code = new StringBuilder();
            code.AppendLine(
@"return (function (__root__, args = {}) {

function __scriptPrivilege__(item) {
  return __svc__.GetScriptPrivilege(item);
}

function __isProxy__(item) {
  return '__isProxy__' in item && item['__isProxy__'];
}

function __wrap__(item) {
  if (__isProxy__(item)) {
    return item;
  }

  return __writable__(__svc__.AsScriptable(item));
}

__wrap__.readonly = function (item) {
  if (__isProxy__(item)) {
    return item;
  }

  return __readable__(__svc__.AsScriptable(item));
}

function __unwrap__(item) {
  if (__isProxy__(item)) {
    return item.element;
  }

  return item;
}

function __readable__(item) {
  if (__scriptPrivilege__(item) === 0) {
    return item;
  }

  return new Proxy(item, {
    has() {
      return true;
    },
    get(target, key) {
      if (typeof key !== 'string') {
        return undefined;
      }

      if (key === '__isProxy__') {
        return true;
      }

      return __readable__(target.Get(key));
    },
    set(target, key, value) { },
    deleteProperty(target, key) { }
  })
}

function __writable__(item) {
  const level = __scriptPrivilege__(item);
  if (level === 0) {
    return item;
  }

  if (typeof level === 'undefined') {
    level = __scriptPrivilege__(item);
  }

  return new Proxy(item, {
    has() { return true },
    get(target, key) {
      if (typeof key !== 'string') {
        return undefined;
      }

      if (key === '__isProxy__') {
        return true;
      }

      return __writable__(target.Get(key));
    },
    set(target, key, value) {
      if (level >= 2 && typeof key === 'string') {
        target.Set(key, value);
      }
    },
    deleteProperty(target, key) {
      if (level >= 3 && typeof key === 'string') {
        target.Delete(key);
      }
    }
  })
}

function __visit__(root, selector, callback, predicates = []) {
  __svc__.VisitScript(__unwrap__(root), selector, function (item) {
    const wrapped = __wrap__(item);
    callback.call(wrapped, wrapped);
  }, ...predicates.map(function (predicate) {
    return function (node) {
      return predicate.call(__wrap__.readonly(node));
    }
  }));
}

function __rule__(root, id) {
  __svc__.VisitRule(__unwrap__(root), id);
}

function __set__(root, key, value) {
  __svc__.Assign(__unwrap__(root), key, value);
}
");

            for (var i = 0; i < stylesheet.Declarations.Count; i++)
            {
                var id = i.ToString();
                var declaration = stylesheet.Declarations[i];
                code.AppendLine(Serialize(id, declaration));
            }

            code.AppendLine("});");

            return new CompiledScript(code.ToString(), new[]
            {
                new ScriptParameter("__svc__", new InjectedServices(stylesheet, dynamicInvoker, asSelectable)),
            });
        }

        public CompiledScript(string code, ScriptParameter[] closure)
        {
            Code = code;
            Closure = closure;
        }

        public string Code { get; }

        public ScriptParameter[] Closure { get; }

        private static string Serialize(string id, StylesheetStatement statement)
        {
            switch (statement)
            {
                case AssignmentStatement assignment:
                    return $"__set__(__root__, {JsonConvert.SerializeObject(assignment.Key)}, {JsonConvert.SerializeObject(assignment.Value)});";
                case ScriptDeclaration scriptDeclaration:
                    return scriptDeclaration.Script;
                case StyleDeclaration style:
                    if (!style.ContainsScripts())
                    {
                        return $"__rule__(__root__, '{id}');";
                    }

                    var predicates = new List<string>();
                    var num = 0;

                    string Replace(ScriptSelector script)
                    {
                        predicates.Add($@"function () {{ return !!({script.Expression}); }}");
                        return $"<{{{num++}}}>";
                    }

                    return $@"__visit__(__root__, {JsonConvert.SerializeObject(style.Selector.Serialize(Replace))}, function (__root__) {{
{string.Join("\n", style.Statements.Select((s, i) => Serialize(id + "." + i, s)))}
}}, [{string.Join(", ", predicates)}]);";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public interface ICompiledStylesheet
    {
        object Apply(object root, object arg);
    }

    internal class ScriptStylesheet : ICompiledStylesheet
    {
        private readonly Func<object, object, object> func;

        public ScriptStylesheet(Func<object, object, object> func)
        {
            this.func = func;
        }

        public object Apply(object root, object arg)
        {
            return func(root, arg);
        }
    }

    internal class SimpleStylesheet : ICompiledStylesheet
    {
        private readonly Stylesheet stylesheet;
        private readonly Func<object, ISelectable> asSelectable;

        public SimpleStylesheet(Stylesheet stylesheet, Func<object, ISelectable> asSelectable)
        {
            this.stylesheet = stylesheet;
            this.asSelectable = asSelectable;
        }

        public object Apply(object root, object arg)
        {
            var selectable = asSelectable(root);
            foreach (var statement in stylesheet.Declarations)
            {
                Accept(selectable, statement);
            }

            return null;
        }

        internal static void Accept(ISelectable selectable, StylesheetStatement statement)
        {
            if (selectable == null)
            {
                return;
            }

            switch (statement)
            {
                case AssignmentStatement assignment:
                    if (selectable is IStyleable styleable)
                    {
                        styleable.Set(assignment.Key, assignment.Value);
                    }

                    break;
                case StyleDeclaration style:
                    SelectorHelper.Visit(selectable, style.Selector, (current, discard1, discard2) =>
                    {
                        foreach (var localStatement in style.Statements)
                        {
                            Accept(current, localStatement);
                        }
                    });

                    break;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
