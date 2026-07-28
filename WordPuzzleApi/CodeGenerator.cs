namespace WordPuzzleApi;

/// <summary>
/// Shared participant-code generation, used by both the word search rounds
/// and the Earth Map Challenge batches so both competitions produce the same
/// A, B, C... style codes.
/// </summary>
public static class CodeGenerator
{
    // 1 -> "A", 2 -> "B", ..., 26 -> "Z", 27 -> "AA", 28 -> "AB", ...
    // (spreadsheet-column style, so any participant count is supported)
    public static string ToLetterCode(int n)
    {
        var chars = new Stack<char>();
        while (n > 0)
        {
            n--;
            chars.Push((char)('A' + n % 26));
            n /= 26;
        }
        return new string(chars.ToArray());
    }
}
