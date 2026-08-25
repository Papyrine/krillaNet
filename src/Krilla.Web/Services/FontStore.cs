namespace Krilla.Web.Services;

/// <summary>
/// Fetches the faces a conversion draws with and builds the <see cref="FontSet"/> from them.
/// </summary>
/// <remarks>
/// <para>
/// krilla has no font database, so a conversion with nothing registered throws rather than
/// quietly producing a blank page. On a desktop that is one <c>AddDirectory</c> call; in a
/// browser there is no directory to read, so each face is fetched over HTTP and handed to
/// <see cref="FontFace.Load(ReadOnlySpan{byte})"/> as bytes.
/// </para>
/// <para>
/// The set is built once and cached. It owns native handles, so it must not be built per
/// conversion — and the faces are two and a half megabytes, which is not a download to repeat.
/// </para>
/// </remarks>
public class FontStore(HttpClient client)
{
    // The four sans styles carry almost every document; the serif and monospace regulars exist so
    // the other two generic families resolve to something of the right shape rather than falling
    // back to sans. Bold serif and bold monospace are deliberately absent: each is another
    // 300-400 KB for a case this app's samples do not reach, and FontSet picks the nearest face.
    /// <summary>The face files this store fetches, relative to <c>wwwroot/fonts</c>.</summary>
    public static readonly string[] Faces =
    [
        "LiberationSans-Regular.ttf",
        "LiberationSans-Bold.ttf",
        "LiberationSans-Italic.ttf",
        "LiberationSans-BoldItalic.ttf",
        "LiberationSerif-Regular.ttf",
        "LiberationMono-Regular.ttf"
    ];

    FontSet? set;

    /// <summary>
    /// The faces, fetched and parsed on first use.
    /// </summary>
    public async Task<FontSet> GetAsync()
    {
        if (set is not null)
        {
            return set;
        }

        // Fetched together rather than in sequence: six requests one after another is six
        // round trips, and the browser will run them in parallel for free.
        var downloads = Faces.Select(_ => client.GetByteArrayAsync($"fonts/{_}"));
        var payloads = await Task.WhenAll(downloads);

        var built = new FontSet();
        foreach (var payload in payloads)
        {
            built.Add(FontFace.Load(payload));
        }

        // Without these three the generic families in a document's CSS resolve to whatever the
        // fallback happens to be, which for an ordinary page means every `font-family: serif`
        // silently rendering as sans. The corpus binds the same three the same way.
        built.SansSerif = "Liberation Sans";
        built.Serif = "Liberation Serif";
        built.Monospace = "Liberation Mono";

        return set = built;
    }
}
