using System.Net;
using System.Net.Sockets;

namespace Farol.Web.Services;

/// <summary>
/// Defesa contra SSRF (Server-Side Request Forgery).
///
/// O Farol faz o servidor visitar uma URL que veio do usuário. Sem filtro, alguém
/// cadastra http://169.254.169.254 (metadados da nuvem), http://localhost:1433
/// (o banco) ou http://10.0.0.5 (rede interna) e usa o servidor como intermediário
/// pra alcançar o que ele mesmo não alcança.
/// </summary>
public static class UrlSafety
{
    /// <summary>
    /// Retorna null quando a URL é aceitável, ou a razão da recusa.
    /// </summary>
    public static async Task<string?> ValidateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "That is not a valid absolute URL.";

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return "Only http:// and https:// are supported.";

        // http://usuario@host esconde o destino real de quem lê rápido.
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return "URLs with credentials are not accepted.";

        // Portas fora das padrão viram varredura de porta interna.
        if (!uri.IsDefaultPort)
            return "Only the default ports (80 and 443) are supported.";

        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(uri.Host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(uri.Host, cancellationToken);
        }
        catch (SocketException)
        {
            return "That host could not be resolved.";
        }

        if (addresses.Length == 0)
            return "That host could not be resolved.";

        // Basta um endereço privado pra recusar: o cliente pode escolher qualquer um.
        if (addresses.Any(IsPrivate))
            return "That address is not publicly routable.";

        return null;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();

            return b[0] switch
            {
                0 => true,                                  // "este host"
                10 => true,                                 // 10.0.0.0/8
                127 => true,                                // loopback
                169 when b[1] == 254 => true,               // link-local — 169.254.169.254
                172 when b[1] >= 16 && b[1] <= 31 => true,  // 172.16.0.0/12
                192 when b[1] == 168 => true,               // 192.168.0.0/16
                100 when b[1] >= 64 && b[1] <= 127 => true, // CGNAT
                >= 224 => true,                             // multicast e reservados
                _ => false
            };
        }

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;

        if (address.Equals(IPAddress.IPv6Any))
            return true;

        // fc00::/7 — unique local
        var bytes = address.GetAddressBytes();
        return (bytes[0] & 0xFE) == 0xFC;
    }
}
