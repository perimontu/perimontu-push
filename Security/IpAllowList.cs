using System.Net;

namespace CoberPush.Api.Security;

/// <summary>Lista de IPs/CIDR permitidas. Una lista vacía no permite a nadie.</summary>
public sealed class IpAllowList
{
    private readonly IReadOnlyList<IPNetwork> _networks;

    private IpAllowList(IReadOnlyList<IPNetwork> networks)
    {
        _networks = networks;
    }

    public static IpAllowList Empty { get; } = new([]);

    /// <summary>Parsea entradas como <c>203.0.113.10</c> o <c>203.0.113.0/24</c> (IPv4 o IPv6).</summary>
    public static bool TryParse(IEnumerable<string> entries, out IpAllowList? list, out string? error)
    {
        var networks = new List<IPNetwork>();
        list = null;
        error = null;

        foreach (var entry in entries)
        {
            if (!TryParseEntry(entry?.Trim(), out var network))
            {
                error = $"IP/CIDR inválido en AllowedSendIps: '{entry}'.";
                return false;
            }

            networks.Add(network);
        }

        list = new IpAllowList(networks);
        return true;
    }

    public bool Contains(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return _networks.Any(n => n.Contains(normalized));
    }

    private static bool TryParseEntry(string? entry, out IPNetwork network)
    {
        network = default;

        if (string.IsNullOrEmpty(entry))
        {
            return false;
        }

        if (entry.Contains('/'))
        {
            return IPNetwork.TryParse(entry, out network);
        }

        if (!IPAddress.TryParse(entry, out var ip))
        {
            return false;
        }

        var normalized = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        var prefix = normalized.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        network = new IPNetwork(normalized, prefix);
        return true;
    }
}
