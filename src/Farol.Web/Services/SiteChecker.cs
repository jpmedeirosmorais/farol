using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Farol.Web.Data;

namespace Farol.Web.Services;

public class SiteChecker(HttpClient http)
{
    public async Task<SiteCheck> CheckAsync(Site site, CancellationToken cancellationToken = default)
    {
        var check = new SiteCheck
        {
            SiteId = site.Id,
            CheckedAt = DateTimeOffset.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, site.Url);
            using var response = await http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var status = (int)response.StatusCode;
            check.StatusCode = status;
            check.IsUp = status < 400;
        }
        catch (HttpRequestException ex)
        {
            check.ErrorMessage = Describe(ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            check.ErrorMessage = "Timed out";
        }

        check.ResponseTimeMs = (int)stopwatch.ElapsedMilliseconds;

        if (Uri.TryCreate(site.Url, UriKind.Absolute, out var uri))
        {
            try
            {
                check.SslExpiresAt = await ReadCertificateExpiryAsync(uri, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Não conseguir ler o certificado não invalida a checagem HTTP.
                // Fica nulo, e a tela mostra "desconhecido".
            }
        }

        return check;
    }

    private static string Describe(HttpRequestException ex) => ex.HttpRequestError switch
    {
        HttpRequestError.NameResolutionError => "DNS did not resolve",
        HttpRequestError.ConnectionError => "Connection refused or unreachable",
        HttpRequestError.SecureConnectionError => "TLS handshake failed",
        _ => $"Request failed: {ex.Message}"
    };

    private static async Task<DateTimeOffset?> ReadCertificateExpiryAsync(
        Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;

        using var tcp = new TcpClient();
        await tcp.ConnectAsync(uri.Host, uri.Port, token);

        await using var ssl = new SslStream(
            tcp.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (_, _, _, _) => true);

        await ssl.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions { TargetHost = uri.Host },
            token);

        if (ssl.RemoteCertificate is X509Certificate2 certificate)
            return new DateTimeOffset(certificate.NotAfter.ToUniversalTime());

        return null;
    }
}