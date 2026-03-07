using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Una.Drawing;

namespace CandyCoat.UI;

/// <summary>
/// Loads fresh Una.Drawing node trees from embedded XML resources.
/// Caches raw XML strings to avoid repeated stream reads, but re-parses
/// on every CreateFromTemplate() call so each BuildNode() gets a new node instance.
///
/// Background: Una.Drawing's template factory forbids hardcoded 'id' attributes inside
/// &lt;template&gt; blocks (they must use ${arg} syntax). To avoid that constraint while
/// keeping the CSS-in-XML benefit, we use plain root nodes (not templates) and re-parse
/// the XML to get a fresh node tree each time.
/// </summary>
internal static class UdtHelper
{
    /// <summary>Cached raw XML strings, keyed by short resource name (e.g. "overview-tab.xml").</summary>
    private static readonly Dictionary<string, string> _xmlStrings = new();
    private static Assembly? _asm;

    public static void Initialize()
    {
        _asm = Assembly.GetExecutingAssembly();
    }

    /// <summary>
    /// Creates a fresh root node from the named XML resource.
    /// The <paramref name="template"/> parameter is accepted for call-site compatibility
    /// but is not used — each XML file has exactly one root node.
    /// </summary>
    public static Node CreateFromTemplate(string resource, string template,
        Dictionary<string, string>? attrs = null)
    {
        var xml = LoadXmlString(resource);
        var doc = UdtLoader.Parse(resource, xml, _asm);

        return doc.RootNode
            ?? throw new InvalidOperationException(
                $"[UdtHelper] XML resource '{resource}' has no root node.");
    }

    /// <summary>Loads the raw XML string for the given resource name, caching it.</summary>
    private static string LoadXmlString(string resourceName)
    {
        if (_xmlStrings.TryGetValue(resourceName, out var cached)) return cached;

        var fullName = _asm!
            .GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException(
                $"[UdtHelper] Embedded resource '{resourceName}' not found in assembly.");

        using var stream = _asm.GetManifestResourceStream(fullName)
            ?? throw new FileNotFoundException(
                $"[UdtHelper] Failed to open stream for resource '{fullName}'.");
        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();
        _xmlStrings[resourceName] = xml;
        return xml;
    }

    public static void ClearCache() => _xmlStrings.Clear();
}
