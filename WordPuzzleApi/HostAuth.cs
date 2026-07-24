namespace WordPuzzleApi;

/// <summary>
/// Guards host-only endpoints (create round, start round) with a shared secret
/// passed in the request body as HostKey. Compared against a value read from
/// configuration/environment — never hard-coded, never committed.
/// </summary>
public static class HostAuth
{
    public static string ExpectedKey(IConfiguration config) =>
        config["HostKey"]
        ?? Environment.GetEnvironmentVariable("WORDPUZZLE_HOST_KEY")
        ?? throw new InvalidOperationException(
            "No host key configured. Set WORDPUZZLE_HOST_KEY (IIS environment variable) or HostKey in configuration.");

    public static bool IsValid(IConfiguration config, string? suppliedKey)
    {
        if (string.IsNullOrEmpty(suppliedKey)) return false;
        var expected = ExpectedKey(config);
        return CryptographicEquals(suppliedKey, expected);
    }

    // Constant-time comparison so response timing doesn't leak how much of the key matched.
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
