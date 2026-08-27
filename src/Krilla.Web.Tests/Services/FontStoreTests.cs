namespace Krilla.Web.Tests.Services;

/// <summary>
/// The font store's caching, which is what stops two overlapping conversions each fetching the
/// faces and each building a <see cref="FontSet"/> that owns native handles.
/// </summary>
public class FontStoreTests
{
    // Counts what actually reaches the transport, and holds every response open so a second caller
    // can arrive while the first is still in flight. That overlap is the whole arrangement the
    // cache exists for, and a sequential test cannot produce it: a field holding the finished set
    // is populated by the time a second sequential call looks at it, so the broken version passes.
    class GatedHandler : HttpMessageHandler
    {
        public int Requests;
        public bool Fail;

        public TaskCompletionSource Gate { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            Cancel cancel)
        {
            Interlocked.Increment(ref Requests);

            await Gate.Task;

            if (Fail)
            {
                throw new HttpRequestException("the transport is refusing");
            }

            var relative = request.RequestUri!.AbsolutePath.TrimStart('/');
            var path = Path.Combine(AppContext.BaseDirectory, relative);

            return new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(await File.ReadAllBytesAsync(path, cancel))
            };
        }
    }

    static (FontStore store, GatedHandler handler) Store()
    {
        var handler = new GatedHandler();
        return (new(new(handler)
        {
            BaseAddress = new("http://localhost/")
        }), handler);
    }

    // The faces are 2.4 MB over six requests. A second conversion starting before the first has
    // finished — the Sample button while a Convert is running — used to fetch all six again and
    // build a second FontSet, orphaning one of them.
    [Test]
    public async Task OverlappingCallsShareOneFetch()
    {
        var (store, handler) = Store();

        var first = store.GetAsync();
        var second = store.GetAsync();

        // Every request is issued before the first await, so this is the count with both callers
        // in flight rather than a count taken afterwards.
        await Assert.That(handler.Requests).IsEqualTo(FontStore.Faces.Length);

        handler.Gate.SetResult();

        await Assert.That(await second).IsSameReferenceAs(await first);
        await Assert.That(handler.Requests).IsEqualTo(FontStore.Faces.Length);
    }

    [Test]
    public async Task CompletedSetIsReused()
    {
        var (store, handler) = Store();
        handler.Gate.SetResult();

        var first = await store.GetAsync();

        await Assert.That(await store.GetAsync()).IsSameReferenceAs(first);
        await Assert.That(handler.Requests).IsEqualTo(FontStore.Faces.Length);
    }

    // Caching the TASK rather than the set makes a failure cacheable too, which would leave the
    // page answering every later conversion with the first transient and needing a reload to
    // recover. A faulted attempt is dropped instead, so the next Convert retries.
    [Test]
    public async Task FailedFetchIsNotCached()
    {
        var (store, handler) = Store();
        handler.Fail = true;
        handler.Gate.SetResult();

        await Assert.That(async () => await store.GetAsync()).Throws<HttpRequestException>();

        handler.Fail = false;

        var set = await store.GetAsync();

        await Assert.That(set).IsNotNull();
        await Assert.That(handler.Requests).IsEqualTo(FontStore.Faces.Length * 2);
    }
}
