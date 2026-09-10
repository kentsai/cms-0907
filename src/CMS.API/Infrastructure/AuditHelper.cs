using System.Collections;
using System.Reflection;

namespace CMS.API.Infrastructure;

public static class AuditHelper
{
    /// <summary>
    /// Compares the public properties that <paramref name="before"/> and <paramref name="after"/> share by name
    /// and returns the names whose values differ, in <paramref name="before"/>'s declaration order. Used to build
    /// the <c>ActionDesc</c> of an UPDATE audit row. Non-string sequences (the N-N pkid lists on some models) are
    /// compared element by element, so two lists with the same members count as unchanged. Properties marked
    /// <see cref="AuditIgnoreAttribute"/> (JOINed labels, subquery counts) are never reported.
    /// </summary>
    public static IReadOnlyList<string> ChangedColumns(object before, object after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var afterProps = after.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var changed = new List<string>();
        foreach (var beforeProp in before.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!beforeProp.CanRead || beforeProp.GetIndexParameters().Length != 0
                || beforeProp.IsDefined(typeof(AuditIgnoreAttribute), inherit: true)
                || !afterProps.TryGetValue(beforeProp.Name, out var afterProp))
            {
                continue;
            }

            if (!ValuesEqual(beforeProp.GetValue(before), afterProp.GetValue(after)))
            {
                changed.Add(beforeProp.Name);
            }
        }

        return changed;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        if (Equals(left, right))
        {
            return true;
        }

        if (left is string || right is string)
        {
            return false;
        }

        if (left is IEnumerable leftItems && right is IEnumerable rightItems)
        {
            return leftItems.Cast<object?>().SequenceEqual(rightItems.Cast<object?>());
        }

        return false;
    }
}
