using System.Reflection;

namespace CMS.API.Infrastructure;

public static class AuditHelper
{
    /// <summary>
    /// Compares the public properties that <paramref name="before"/> and <paramref name="after"/> share by name
    /// and returns the names whose values differ. Used to build the <c>ActionDesc</c> of an UPDATE audit row.
    /// </summary>
    public static IReadOnlyList<string> ChangedColumns(object before, object after)
    {
        var afterProps = after.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var changed = new List<string>();
        foreach (var beforeProp in before.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!beforeProp.CanRead || !afterProps.TryGetValue(beforeProp.Name, out var afterProp))
            {
                continue;
            }

            if (!Equals(beforeProp.GetValue(before), afterProp.GetValue(after)))
            {
                changed.Add(beforeProp.Name);
            }
        }

        return changed;
    }
}
