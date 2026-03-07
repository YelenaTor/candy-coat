using System.Collections.Generic;
using System.Reflection;
using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Caches loaded UdtDocuments by resource name to avoid re-parsing on every BuildNode() call.
/// Initialize() must be called once after DrawingLib.Setup().
/// </summary>
internal static class UdtHelper
{
    private static readonly Dictionary<string, UdtDocument> _cache = new();
    private static Assembly? _asm;

    public static void Initialize()
    {
        _asm = Assembly.GetExecutingAssembly();
    }

    public static UdtDocument Load(string resourceName)
    {
        if (_cache.TryGetValue(resourceName, out var cached)) return cached;
        var doc = UdtLoader.LoadFromAssembly(_asm!, resourceName);
        _cache[resourceName] = doc;
        return doc;
    }

    /// <summary>
    /// Creates a fresh node instance from a named template in the specified resource.
    /// Each call returns a new node tree (not the cached RootNode).
    /// </summary>
    public static Node CreateFromTemplate(string resource, string template,
        Dictionary<string, string>? attrs = null)
        => Load(resource).CreateNodeFromTemplate(template, attrs);

    public static void ClearCache() => _cache.Clear();
}
