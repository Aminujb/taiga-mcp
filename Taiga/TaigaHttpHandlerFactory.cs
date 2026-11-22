using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TaigaMcp.Configuration;

namespace TaigaMcp.Taiga;

/// <summary>
/// Creates the HTTP handler for the Taiga API and handles corporate or self-signed
/// certificates:
///  - CaCertificatePath    - trust the given certificate (pinned by thumbprint; a root CA through the chain);
///  - IgnoreSslErrors=true - disable validation entirely (last resort, trusted networks only).
/// </summary>
public static class TaigaHttpHandlerFactory
{
    /// <summary>
    /// Validates the certificate when the application initializes.
    /// Throws if the certificate is missing, corrupt or expired,
    /// so a configuration error is caught at startup, not on the first request.
    /// </summary>
    public static void ValidateConfiguration(TaigaOptions options)
    {
        // With validation disabled or no certificate path, there is nothing to check.
        if (options.IgnoreSslErrors || string.IsNullOrWhiteSpace(options.CaCertificatePath))
        {
            return;
        }

        using var certificate = LoadCertificate(options.CaCertificatePath);
    }

    /// <summary>Creates an HTTP handler for the given Taiga settings.</summary>
    public static HttpClientHandler Create(TaigaOptions options)
    {
        var handler = new HttpClientHandler();

        if (options.IgnoreSslErrors)
        {
            // WARNING: this disables certificate validation. Use only for trusted internal servers.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        }

        if (!string.IsNullOrWhiteSpace(options.CaCertificatePath))
        {
            // Only the thumbprint is needed for the check, so the certificate itself is disposed.
            string pinnedThumbprint;
            using (var pinned = LoadCertificate(options.CaCertificatePath))
            {
                pinnedThumbprint = pinned.Thumbprint;
            }

            handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                // If standard validation already succeeded, trust it.
                if (errors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (certificate is null)
                {
                    return false;
                }

                // 1) Exact certificate match (pinning), which works for a
                //    self-signed or leaf certificate.
                return string.Equals(certificate.Thumbprint, pinnedThumbprint, StringComparison.OrdinalIgnoreCase);
            };
        }

        return handler;
    }

    /// <summary>
    /// Loads and validates a certificate from a file. Supports PEM (.pem/.crt)
    /// and DER/PKCS#12 (.cer/.pfx). A relative path is resolved from the build output directory.
    /// </summary>
    private static X509Certificate2 LoadCertificate(string path)
    {
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Corporate CA certificate not found: {fullPath}", fullPath);
        }

        X509Certificate2 certificate;
        try
        {
            // In .NET 8 the X509Certificate2(path) constructor supports PEM (.crt/.pem)
            // and DER/PKCS#12 (.cer/.pfx).
            certificate = new X509Certificate2(fullPath);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                $"Failed to load certificate '{fullPath}': the file is corrupt or its format is not supported.", ex);
        }

        if (string.IsNullOrEmpty(certificate.Thumbprint))
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Certificate '{fullPath}' contains no valid data (empty thumbprint).");
        }

        if (certificate.NotAfter <= DateTime.Now)
        {
            var notAfter = certificate.NotAfter;
            certificate.Dispose();
            throw new InvalidOperationException(
                $"Certificate '{fullPath}' expired on {notAfter:u}.");
        }

        return certificate;
    }
}
