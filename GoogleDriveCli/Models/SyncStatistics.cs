namespace GoogleDriveCli.Models;

public class SyncStatistics
{
    private int _downloaded;
    private int _failed;
    private int _skipped;

    public int Downloaded => _downloaded;
    public int Failed => _failed;
    public int Skipped => _skipped;

    public void IncrementDownloaded() => Interlocked.Increment(ref _downloaded);
    public void IncrementFailed() => Interlocked.Increment(ref _failed);
    public void IncrementSkipped() => Interlocked.Increment(ref _skipped);
}