using System.Net.Http;
using System.Xml;
using System.Xml.Linq;

namespace AiteBar;

internal static class RssFeedReader
{
    private const int MaximumBytes = 1024 * 1024;
    private const int MaximumItems = 100;
    private static readonly HttpClient Http = new();
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Rss1 = "http://purl.org/rss/1.0/";

    internal sealed record Entry(string Title, Uri? Link)
    {
        internal string PromptText => Link is null ? Title : $"{Title}{Environment.NewLine}{Link.AbsoluteUri}";
    }

    internal static async Task<IReadOnlyList<Entry>?> ReadAsync(string address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) || !IsWebLink(uri)) return null;
        using var timed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timed.CancelAfter(TimeSpan.FromSeconds(12));
        using HttpResponseMessage response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timed.Token);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaximumBytes) return null;
        await using Stream stream = await response.Content.ReadAsStreamAsync(timed.Token);
        byte[] body = new byte[MaximumBytes + 1];
        int count = await stream.ReadAtLeastAsync(body, body.Length, throwOnEndOfStream: false, timed.Token);
        if (count > MaximumBytes) return null;
        using var copy = new MemoryStream(body, 0, count);
        return Read(copy, response.RequestMessage?.RequestUri ?? uri);
    }

    internal static IReadOnlyList<Entry>? Read(Stream stream, Uri? baseUri = null)
    {
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes
        });
        try
        {
            var root = XDocument.Load(reader).Root;
            XNamespace itemNamespace;
            IEnumerable<XElement> elements;
            if (root?.Name == "rss" && root.Element("channel") is { } channel)
            {
                itemNamespace = XNamespace.None;
                elements = channel.Elements("item");
            }
            else if (root?.Name == Atom + "feed")
            {
                itemNamespace = Atom;
                elements = root.Elements(Atom + "entry");
            }
            else if (root?.Name == Rdf + "RDF" && root.Element(Rss1 + "channel") is not null)
            {
                itemNamespace = Rss1;
                elements = root.Elements(Rss1 + "item");
            }
            else return null;

            return elements.Select(item => ReadEntry(item, itemNamespace, baseUri))
                .OfType<Entry>().Take(MaximumItems).ToArray();
        }
        catch (XmlException) { return null; }
    }

    private static Entry? ReadEntry(XElement item, XNamespace itemNamespace, Uri? baseUri)
    {
        string title = ((string?)item.Element(itemNamespace + "title") ?? "").Trim();
        XElement? linkElement = itemNamespace == Atom
            ? item.Elements(Atom + "link").FirstOrDefault(element => (string?)element.Attribute("rel") is null or "alternate")
            : item.Element(itemNamespace + "link");
        string? target = itemNamespace == Atom ? (string?)linkElement?.Attribute("href") : (string?)linkElement;
        Uri? link = null;
        if (!string.IsNullOrWhiteSpace(target))
        {
            foreach (var ancestor in (linkElement ?? item).AncestorsAndSelf().Reverse())
            {
                if (ancestor.Attribute(XNamespace.Xml + "base") is { } setting &&
                    ResolveUri(baseUri, setting.Value) is { } candidate) baseUri = candidate;
            }
            link = ResolveUri(baseUri, target.Trim());
        }
        if (title.Length == 0 && link is null) return null;
        return new Entry(title.Length == 0 ? link!.AbsoluteUri : title, title.Length == 0 ? null : link);
    }

    private static Uri? ResolveUri(Uri? baseUri, string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute) && IsWebLink(absolute)) return absolute;
        return baseUri is not null && Uri.TryCreate(baseUri, value, out Uri? combined) && IsWebLink(combined) ? combined : null;
    }

    private static bool IsWebLink(Uri uri) => uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0;
}
