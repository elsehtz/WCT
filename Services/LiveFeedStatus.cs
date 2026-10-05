namespace WorldCupTerminal.Services;

/// <summary>
/// Mutable singleton the scheduler writes and the UI (alerts, footer) reads: which mode the
/// site is in, when the feed last succeeded and when it will try again.
/// </summary>
public class LiveFeedStatus
{
    public DataMode Mode { get; set; } = DataMode.Simulated;
    public DateTime? LastSuccessUtc { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRefreshUtc { get; set; }
}
