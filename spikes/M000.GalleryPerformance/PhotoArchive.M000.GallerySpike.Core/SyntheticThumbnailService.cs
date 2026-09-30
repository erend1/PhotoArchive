namespace PhotoArchive.M000.GallerySpike.Core;

public readonly record struct SyntheticThumbnail(byte R, byte G, byte B, string Label);

public sealed class SyntheticThumbnailService
{
    private int _requested;
    private int _completed;
    private int _cancelled;
    private int _inFlight;
    private int _peakInFlight;

    public int Requested => Volatile.Read(ref _requested);
    public int Completed => Volatile.Read(ref _completed);
    public int Cancelled => Volatile.Read(ref _cancelled);
    public int InFlight => Volatile.Read(ref _inFlight);
    public int PeakInFlight => Volatile.Read(ref _peakInFlight);

    public async Task<SyntheticThumbnail> LoadAsync(SyntheticAssetRecord asset, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requested);
        var inFlight = Interlocked.Increment(ref _inFlight);
        UpdatePeak(inFlight);

        try
        {
            // Deterministic delay models thumbnail-cache/decode work without reading media files.
            await Task.Delay(60 + asset.Index % 90, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var hash = unchecked((uint)(asset.Index * 2246822519u + 3266489917u));
            var result = new SyntheticThumbnail(
                (byte)(64 + (hash & 0x7F)),
                (byte)(64 + ((hash >> 8) & 0x7F)),
                (byte)(64 + ((hash >> 16) & 0x7F)),
                asset.IsVideo ? "VIDEO" : "PHOTO");

            Interlocked.Increment(ref _completed);
            return result;
        }
        catch (OperationCanceledException)
        {
            Interlocked.Increment(ref _cancelled);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private void UpdatePeak(int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref _peakInFlight);
            if (candidate <= current || Interlocked.CompareExchange(ref _peakInFlight, candidate, current) == current)
            {
                return;
            }
        }
    }
}
