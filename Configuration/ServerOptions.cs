namespace TaigaMcp.Configuration;

/// <summary>
/// Web server settings (listen address and port).
/// Read from the "Server" section and can be overridden by MCP_HOST / MCP_PORT.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Listen address.
    /// "127.0.0.1" (default) is local only, "0.0.0.0" is all network interfaces;
    /// "localhost", "*", "+" and a specific IP are also supported.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Listen port.</summary>
    public int Port { get; set; } = 5000;
}
