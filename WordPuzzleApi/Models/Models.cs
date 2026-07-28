namespace WordPuzzleApi.Models;

public class Round
{
    public string Seed { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Category { get; set; } = "";
    public int GridSize { get; set; }
    public string WordsCsv { get; set; } = "";
    public int ParticipantCount { get; set; }
    public byte Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
}

public class ParticipantCode
{
    public int Id { get; set; }
    public string Seed { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsClaimed { get; set; }
    public string? ClaimedUnit { get; set; }
    public string? ClaimedName { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
}

public class ResultEntry
{
    public int Id { get; set; }
    public int ParticipantCodeId { get; set; }
    public string Seed { get; set; } = "";
    public int WordsFound { get; set; }
    public int TotalWords { get; set; }
    public int TimeSeconds { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
}

// ---- Request/response DTOs ----

public record CreateRoundRequest(
    string Seed,
    string Tier,
    string Category,
    int GridSize,
    string[] Words,
    int ParticipantCount,
    string HostKey);

public record CreateRoundResponse(string Seed, string[] Codes);

public record StartRoundRequest(string HostKey);

public record StartRoundResponse(DateTime StartedAtUtc);

public record RoundStatusResponse(byte Status, DateTime? StartedAtUtc, DateTime ServerTimeUtc);

public record ClaimRequest(string Code);

public record ClaimResponse(bool Claimed, int ParticipantCodeId);

public record SubmitResultRequest(string Code, int WordsFound, int TotalWords, int TimeSeconds);

public record SubmitResultResponse(bool Accepted, int Rank);

public record LeaderboardRow(int Rank, string Code, int WordsFound, int TotalWords, int TimeSeconds);

public record KvGetResponse(string Value);

public record KvSetRequest(string Value);

// ---- Earth Map Challenge ----

public class EarthMapBatch
{
    public string BatchKey { get; set; } = "";
    public int ParticipantCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class EarthMapCode
{
    public int Id { get; set; }
    public string BatchKey { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsClaimed { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
}

public record CreateBatchRequest(int ParticipantCount, string Username, string Password);

public record CreateBatchResponse(string BatchKey, string[] Codes);

public record CurrentBatchResponse(string? BatchKey);

public record EarthMapClaimRequest(string Code);

public record EarthMapClaimResponse(bool Claimed, int CodeId);

public record SubmitEarthMapResultRequest(string Code, int Score, int LocationCount);

public record SubmitEarthMapResultResponse(bool Accepted, int Rank);

public record EarthMapLeaderboardRow(int Rank, string Code, int Score, int LocationCount, DateTime SubmittedAtUtc);

public record ClearResultsRequest(string Username, string Password);

public record AdminLoginRequest(string Username, string Password);

public record AdminLoginResponse(bool Success);
