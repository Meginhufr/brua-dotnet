namespace Brua.Web;

/// <summary>Why a picture has none to show. The same reasons on every platform's SDK.</summary>
public abstract record BruaImageError
{
    private BruaImageError() { }

    /// <summary>Nothing is released under the id: the edge answered 404. Usually a call site draws nothing.</summary>
    public sealed record NotReleased : BruaImageError;

    /// <summary>The edge can't be reached and no release of the id is known.</summary>
    /// <param name="Cause">What went wrong reading it.</param>
    public sealed record Unreachable(Exception Cause) : BruaImageError;

    /// <summary>The asset is known, but its file failed to load in the browser.</summary>
    /// <param name="Url">The file that failed.</param>
    public sealed record File(string Url) : BruaImageError;
}
