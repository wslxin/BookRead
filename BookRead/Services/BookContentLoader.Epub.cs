using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 提供 EPUB 文档的正文提取和章节生成逻辑。
/// </summary>
internal static partial class BookContentLoader
{
    /// <summary>
    /// 按照 EPUB 的 OPF spine 顺序读取正文文档并生成章节。
    /// </summary>
    /// <param name="filePath">EPUB 文件路径。</param>
    /// <returns>按 spine 顺序排列的章节列表。</returns>
    /// <exception cref="InvalidDataException">压缩包结构无效时抛出。</exception>
    /// <exception cref="BookContentLoadException">缺少必要文件或没有可读取正文时抛出。</exception>
    private static async Task<IReadOnlyList<BookChapter>> LoadEpubAsync(string filePath)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);

        ZipArchiveEntry containerEntry = GetRequiredEntry(archive, "META-INF/container.xml");
        XDocument containerDocument;
        await using (Stream containerStream = containerEntry.Open())
        {
            containerDocument = LoadXml(containerStream);
        }

        string? packagePath = containerDocument
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "rootfile")
            ?.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "full-path")
            ?.Value;
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new BookContentLoadException("EPUB 缺少 OPF 包文档路径。");
        }

        ZipArchiveEntry packageEntry = GetRequiredEntry(archive, packagePath);
        XDocument packageDocument;
        await using (Stream packageStream = packageEntry.Open())
        {
            packageDocument = LoadXml(packageStream);
        }

        Dictionary<string, XElement> manifestItems = ReadEpubManifest(packageDocument);
        List<string> spineIds = packageDocument
            .Descendants()
            .Where(element => element.Name.LocalName == "itemref")
            .Select(element => element.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "idref")
                ?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToList();
        if (spineIds.Count == 0)
        {
            throw new BookContentLoadException("EPUB 中没有可读取的阅读顺序。");
        }

        var chapters = new List<BookChapter>();
        foreach (string itemId in spineIds)
        {
            if (!manifestItems.TryGetValue(itemId, out XElement? manifestItem))
            {
                continue;
            }

            string? mediaType = manifestItem.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "media-type")
                ?.Value;
            if (!IsEpubTextMediaType(mediaType))
            {
                continue;
            }

            string? href = manifestItem.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "href")
                ?.Value;
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            string entryPath = ResolveArchivePath(packagePath, href);
            ZipArchiveEntry? contentEntry = FindEntry(archive, entryPath);
            if (contentEntry is null)
            {
                continue;
            }

            string html = await ReadZipEntryTextAsync(contentEntry);
            string title = ExtractHtmlTitle(html) ?? $"第 {chapters.Count + 1} 章";
            string content = RemoveLeadingTitle(ExtractHtmlText(html), title);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            chapters.Add(new BookChapter(title, content));
        }

        if (chapters.Count == 0)
        {
            throw new BookContentLoadException("EPUB 中没有可读取的正文。");
        }

        return chapters;
    }

    /// <summary>
    /// 从 OPF 包文档中读取清单项，并按 id 建立索引。
    /// </summary>
    /// <param name="packageDocument">已解析的 OPF 文档。</param>
    /// <returns>清单 id 与 XML 元素的映射。</returns>
    private static Dictionary<string, XElement> ReadEpubManifest(XDocument packageDocument)
    {
        var manifestItems = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (XElement item in packageDocument.Descendants().Where(element => element.Name.LocalName == "item"))
        {
            string? id = item.Attributes()
                .FirstOrDefault(attribute => attribute.Name.LocalName == "id")
                ?.Value;
            if (!string.IsNullOrWhiteSpace(id))
            {
                manifestItems.TryAdd(id, item);
            }
        }

        return manifestItems;
    }

    /// <summary>
    /// 判断 EPUB 清单项的媒体类型是否可以作为 XHTML/HTML 正文读取。
    /// </summary>
    /// <param name="mediaType">清单项声明的媒体类型。</param>
    /// <returns>可以作为正文读取时返回 true，否则返回 false。</returns>
    private static bool IsEpubTextMediaType(string? mediaType)
    {
        return string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 根据 OPF 所在目录解析 spine 文档的压缩包内部路径。
    /// </summary>
    /// <param name="packagePath">OPF 包文档路径。</param>
    /// <param name="href">清单中声明的文档地址。</param>
    /// <returns>规范化后的压缩包内部路径。</returns>
    private static string ResolveArchivePath(string packagePath, string href)
    {
        int fragmentIndex = href.IndexOfAny(['#', '?']);
        string pathPart = fragmentIndex >= 0 ? href[..fragmentIndex] : href;
        pathPart = Uri.UnescapeDataString(pathPart.Replace('\\', '/'));

        string packageDirectory = Path.GetDirectoryName(
            packagePath.Replace('/', Path.DirectorySeparatorChar))?.Replace('\\', '/') ?? string.Empty;
        string baseUriText = string.IsNullOrWhiteSpace(packageDirectory)
            ? "https://bookread.local/"
            : $"https://bookread.local/{packageDirectory.Trim('/')}/";
        var resolvedUri = new Uri(new Uri(baseUriText), pathPart);
        return Uri.UnescapeDataString(resolvedUri.AbsolutePath.TrimStart('/'));
    }
}
