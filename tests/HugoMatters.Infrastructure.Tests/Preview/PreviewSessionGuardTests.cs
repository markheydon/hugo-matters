using HugoMatters.Infrastructure.Preview;

namespace HugoMatters.Infrastructure.Tests.Preview;

public sealed class PreviewSessionGuardTests
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void BeginOperation_SupersedesPriorGenerationAndCancelsPreviousToken()
    {
        var guard = new PreviewSessionGuard();

        var (firstGeneration, firstToken) = guard.BeginOperation(SessionId);
        var (secondGeneration, secondToken) = guard.BeginOperation(SessionId);

        Assert.NotEqual(firstGeneration, secondGeneration);
        Assert.True(guard.IsCurrent(SessionId, secondGeneration));
        Assert.False(guard.IsCurrent(SessionId, firstGeneration));
        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
    }

    [Fact]
    public void Invalidate_SupersedesActiveGenerationAndCancelsToken()
    {
        var guard = new PreviewSessionGuard();

        var (generation, token) = guard.BeginOperation(SessionId);
        guard.Invalidate(SessionId);

        Assert.False(guard.IsCurrent(SessionId, generation));
        Assert.True(token.IsCancellationRequested);
    }
}
