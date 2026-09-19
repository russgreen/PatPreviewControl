namespace FillPatternPreview.Rendering;

/// <summary>
/// An aggregate work allowance for one render pass. The per-family and per-chord caps bound
/// each piece of work, but a pattern with many families (or an extreme zoom) can still add up
/// to tens of millions of draw calls on the UI thread; the budget bounds the total. When it runs
/// out the remaining lines are simply not drawn, which is far better than freezing the host.
/// Each visible chord and each dash segment costs one unit.
/// </summary>
internal sealed class RenderBudget
{
    public const int DefaultLimit = 250_000;

    private readonly int _limit;
    private int _remaining;

    public RenderBudget(int limit = DefaultLimit)
    {
        _limit = limit;
        _remaining = limit;
    }

    public bool IsExhausted => _remaining <= 0;

    /// <summary>Takes one unit; returns false (and takes nothing) once the budget is spent.</summary>
    public bool TryConsume()
    {
        if (_remaining <= 0)
        {
            return false;
        }

        _remaining--;
        return true;
    }

    public void Reset() => _remaining = _limit;
}
