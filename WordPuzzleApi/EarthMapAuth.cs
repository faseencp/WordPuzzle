namespace WordPuzzleApi;

/// <summary>
/// Guards the Earth Map Challenge admin area (login, create batch, clear
/// results) with a dedicated username/password, separate from the word
/// search HostKey. Compared against values read from configuration/
/// environment — never hard-coded, never committed.
/// </summary>
public static class EarthMapAuth
{
    public static string ExpectedUsername(IConfiguration config) =>
        config["EarthMapAdminUsername"]
        ?? Environment.GetEnvironmentVariable("EARTHMAP_ADMIN_USERNAME")
        ?? throw new InvalidOperationException(
            "No admin username configured. Set EARTHMAP_ADMIN_USERNAME (IIS environment variable).");

    public static string ExpectedPassword(IConfiguration config) =>
        config["EarthMapAdminPassword"]
        ?? Environment.GetEnvironmentVariable("EARTHMAP_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException(
            "No admin password configured. Set EARTHMAP_ADMIN_PASSWORD (IIS environment variable).");

    public static bool IsValid(IConfiguration config, string? username, string? password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;
        return CryptographicEquals(username, ExpectedUsername(config))
            && CryptographicEquals(password, ExpectedPassword(config));
    }

    // Constant-time comparison so response timing doesn't leak how much matched.
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
