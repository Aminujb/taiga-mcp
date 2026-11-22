using System.Collections.Concurrent;
using System.Reflection;
using ModelContextProtocol.Server;

namespace TaigaMcp.Metrics;

/// <summary>
/// Resolves the MCP tool name (the value of <c>McpServerToolAttribute.Name</c>)
/// from the tool's class and method name. The result is cached.
/// </summary>
public static class ToolNameResolver
{
    private static readonly ConcurrentDictionary<(Type Type, string Method), string> Cache = new();

    /// <summary>Returns the MCP tool name for a class method.</summary>
    public static string Resolve(Type toolClass, string methodName)
        => Cache.GetOrAdd((toolClass, methodName), static key => ResolveCore(key.Type, key.Method));

    private static string ResolveCore(Type toolClass, string methodName)
    {
        var method = toolClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        var attribute = method?.GetCustomAttribute<McpServerToolAttribute>();
        return attribute?.Name ?? methodName;
    }
}
