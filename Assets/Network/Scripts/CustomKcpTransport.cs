using System.Linq;
using System;
using System.Net;
using System.Net.Sockets;
using kcp2k;
using UnityEngine;

public class CustomKcpTransport : KcpTransport
{
    public override Uri ServerUri()
    {
        // Get all addresses for this host.
        IPAddress[] addresses = Dns.GetHostAddresses(Dns.GetHostName());

        // Pick the first IPv4 address that isn't loopback (127.x.x.x) or link-local (169.254.x.x).
        var ipAddress = addresses.FirstOrDefault(
            ip => ip.AddressFamily == AddressFamily.InterNetwork &&
                  !IPAddress.IsLoopback(ip) &&
                  !IsAPIPA(ip)
        );

        // Fallback to loopback if none found.
        if (ipAddress == null)
        {
            Debug.LogWarning("MyKcpTransport: No valid LAN IPv4 found, falling back to 127.0.0.1");
            ipAddress = IPAddress.Loopback;
        }

        UriBuilder builder = new UriBuilder
        {
            Scheme = "kcp",
            Host = ipAddress.ToString(),
            Port = Port
        };

        return builder.Uri;
    }

    // Helper to detect APIPA (169.254.x.x) addresses
    private bool IsAPIPA(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }
}