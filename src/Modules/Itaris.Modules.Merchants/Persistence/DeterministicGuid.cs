using System.Security.Cryptography;
using System.Text;

namespace Itaris.Modules.Merchants.Persistence;

/// <summary>
/// Stable, name-derived GUIDs for seed rows (RFC 4122 v5, SHA-1 over a fixed namespace).
/// Lets seeded permissions/roles keep the same IDs across environments and re-runs so
/// role_permissions references stay valid without hardcoding dozens of literal GUIDs.
/// </summary>
public static class DeterministicGuid
{
    // Fixed namespace GUID for Itaris seed data (arbitrary but constant).
    private static readonly byte[] Namespace = new Guid("6f9c1e2a-8b3d-4c7e-9a1f-2d5c8a11aa01").ToByteArray();

    public static Guid Create(string name)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hash = SHA1.HashData([.. Namespace, .. nameBytes]);

        var guid = new byte[16];
        Array.Copy(hash, guid, 16);
        guid[6] = (byte)((guid[6] & 0x0F) | 0x50); // version 5
        guid[8] = (byte)((guid[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(guid);
    }
}
