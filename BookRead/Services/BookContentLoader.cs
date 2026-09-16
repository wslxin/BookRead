using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 根据文件扩展名读取书籍，并将文档内容转换为统一的章节列表。
/// </summary>
internal static partial class BookContentLoader
{
    private static readonly Regex HtmlHeadRegex = new(
        @"<head\b[^>]*>.*?</head>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlScriptStyleRegex = new(
        @"<(script|style)\b[^>]*>.*?</\1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlBreakRegex = new(
        @"<br\s*/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlBlockBoundaryRegex = new(
        @"</?(?:p|div|li|ul|ol|h[1-6]|section|article|blockquote|tr|table|pre)\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex HtmlH1Regex = new(
        @"<h1\b[^>]*>(?<title>.*?)</h1>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlH2Regex = new(
        @"<h2\b[^>]*>(?<title>.*?)</h2>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex HtmlDocumentTitleRegex = new(
        @"<title\b[^>]*>(?<title>.*?)</title>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);


    private static readonly Regex HtmlCommentRegex = new(
        @"<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// 读取指定书籍文件，并按扩展名提取正文和章节。
    /// </summary>
    /// <param name="filePath">要读取的书籍文件绝对路径。</param>
    /// <returns>按原文顺序排列的章节列表。</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> 为空时抛出。</exception>
    /// <exception cref="FileNotFoundException">书籍文件不存在时抛出。</exception>
    /// <exception cref="BookContentLoadException">格式不受支持、文件损坏或没有可读取正文时抛出。</exception>
    internal static async Task<IReadOnlyList<BookChapter>> LoadAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("书籍文件不存在。", filePath);
        }

        string extension = Path.GetExtension(filePath);
        try
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" or ".text" => await LoadPlainTextAsync(filePath),
                ".md" or ".markdown" => await LoadMarkdownAsync(filePath),
                ".html" or ".htm" => await LoadHtmlAsync(filePath),
                ".epub" => await LoadEpubAsync(filePath),
                ".docx" => await LoadDocxAsync(filePath),
                _ => throw new BookContentLoadException($"暂不支持“{extension}”格式的书籍文件。")
            };
        }
        catch (BookContentLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // 文档内容来自用户文件，结构可能任意损坏；统一转为可展示错误，避免未处理异常结束应用。
            throw new BookContentLoadException($"无法读取书籍：{exception.Message}", exception);
        }
    }

    /// <summary>
    /// 读取纯文本文件并使用章节解析器拆分正文。
    /// </summary>
    /// <param name="filePath">文本文件路径。</param>
    /// <returns>解析后的章节列表。</returns>
    private static async Task<IReadOnlyList<BookChapter>> LoadPlainTextAsync(string filePath)
    {
        string content = await ReadTextFileAsync(filePath);
        return ParsePlainText(content);
    }

    /// <summary>
    /// 读取 Markdown 文件，保留格式标记并按 Markdown 标题拆分正文。
    /// </summary>
    /// <param name="filePath">Markdown 文件路径。</param>
    /// <returns>解析后的章节列表。</returns>
    /// <exception cref="BookContentLoadException">正文为空时抛出。</exception>
    private static async Task<IReadOnlyList<BookChapter>> LoadMarkdownAsync(string filePath)
    {
        string markdown = await ReadTextFileAsync(filePath);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new BookContentLoadException("文件中没有可读取的正文。");
        }

        return ChapterParser.ParseMarkdown(markdown);
    }

    /// <summary>
    /// 读取 HTML 文件，将可见文本转换为纯文本并拆分正文。
    /// </summary>
    /// <param name="filePath">HTML 文件路径。</param>
    /// <returns>解析后的章节列表。</returns>
    private static async Task<IReadOnlyList<BookChapter>> LoadHtmlAsync(string filePath)
    {
        string html = await ReadTextFileAsync(filePath);
        string content = ExtractHtmlText(html);
        return ParsePlainText(content);
    }

    /// <summary>
    /// 检查纯文本内容并交给现有章节解析器处理。
    /// </summary>
    /// <param name="content">要解析的正文。</param>
    /// <returns>解析后的章节列表。</returns>
    /// <exception cref="BookContentLoadException">正文为空时抛出。</exception>
    private static IReadOnlyList<BookChapter> ParsePlainText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BookContentLoadException("文件中没有可读取的正文。");
        }

        return ChapterParser.Parse(content);
    }


    /// <summary>
    /// 从 HTML 中提取可见文本，移除脚本、样式和标签。
    /// </summary>
    /// <param name="html">原始 HTML 内容。</param>
    /// <returns>规范化后的纯文本。</returns>
    private static string ExtractHtmlText(string html)
    {
        // 先移除文档头和脚本样式，避免标题或脚本内容混入阅读正文。
        string content = HtmlHeadRegex.Replace(html, string.Empty);
        content = HtmlScriptStyleRegex.Replace(content, string.Empty);
        content = HtmlCommentRegex.Replace(content, string.Empty);
        content = HtmlBreakRegex.Replace(content, "\n");
        content = HtmlBlockBoundaryRegex.Replace(content, "\n");
        content = HtmlTagRegex.Replace(content, string.Empty);
        content = WebUtility.HtmlDecode(content);
        return NormalizeExtractedText(content);
    }

    /// <summary>
    /// 从 HTML 中依次提取首个 h1、h2 或 title 作为章节标题。
    /// </summary>
    /// <param name="html">原始 HTML 内容。</param>
    /// <returns>找到的标题文本；不存在时返回 <see langword="null"/>。</returns>
    private static string? ExtractHtmlTitle(string html)
    {
        foreach (Regex titleRegex in new[] { HtmlH1Regex, HtmlH2Regex, HtmlDocumentTitleRegex })
        {
            foreach (Match match in titleRegex.Matches(html))
            {
                string title = HtmlTagRegex.Replace(match.Groups["title"].Value, string.Empty);
                title = NormalizeExtractedText(WebUtility.HtmlDecode(title));
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 移除章节正文开头与章节标题重复的标题行。
    /// </summary>
    /// <param name="content">章节正文。</param>
    /// <param name="title">章节标题。</param>
    /// <returns>移除重复标题后的正文。</returns>
    private static string RemoveLeadingTitle(string content, string title)
    {
        if (!content.StartsWith(title, StringComparison.Ordinal))
        {
            return content;
        }

        return content[title.Length..].TrimStart();
    }

    /// <summary>
    /// 统一换行和空白，删除连续空行并去除每行首尾空白。
    /// </summary>
    /// <param name="content">要规范化的文本。</param>
    /// <returns>规范化后的文本。</returns>
    private static string NormalizeExtractedText(string content)
    {
        string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var builder = new StringBuilder(normalized.Length);
        bool previousLineWasEmpty = false;

        foreach (string line in normalized.Split('\n'))
        {
            string trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                if (builder.Length > 0 && !previousLineWasEmpty)
                {
                    builder.AppendLine();
                }

                previousLineWasEmpty = true;
                continue;
            }

            builder.AppendLine(trimmedLine);
            previousLineWasEmpty = false;
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// 使用文件 BOM 或内容特征检测编码并异步读取完整文本。
    /// </summary>
    /// <param name="filePath">要读取的文本文件路径。</param>
    /// <returns>文件中的完整文本。</returns>
    /// <exception cref="IOException">读取文件失败时抛出。</exception>
    /// <exception cref="UnauthorizedAccessException">没有权限读取文件时抛出。</exception>
    private static async Task<string> ReadTextFileAsync(string filePath)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var reader = new StreamReader(
            stream,
            DetectEncoding(stream),
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 64 * 1024);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 读取 ZIP 条目，并按 BOM、UTF-8 或 GB18030 顺序解码文本。
    /// </summary>
    /// <param name="entry">要读取的 ZIP 条目。</param>
    /// <returns>条目中的完整文本。</returns>
    /// <exception cref="IOException">读取条目失败时抛出。</exception>
    private static async Task<string> ReadZipEntryTextAsync(ZipArchiveEntry entry)
    {
        await using Stream entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        await entryStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var reader = new StreamReader(
            memoryStream,
            DetectEncoding(memoryStream),
            detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 根据文件 BOM 选择文本编码，未带 BOM 时优先 UTF-8，否则使用 GB18030。
    /// </summary>
    /// <param name="stream">待检测的可定位文件流，调用后位置会恢复。</param>
    /// <returns>检测到的文本编码。</returns>
    /// <exception cref="IOException">读取文件流失败时抛出。</exception>
    private static Encoding DetectEncoding(Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new IOException("无法检测不可定位流的文本编码。");
        }

        long originalPosition = stream.Position;
        Span<byte> bom = stackalloc byte[4];
        int read = stream.Read(bom);
        stream.Position = originalPosition;

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
        {
            return new UTF8Encoding(false);
        }

        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
        {
            return Encoding.Unicode;
        }

        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode;
        }

        int sampleLength = (int)Math.Min(stream.Length - originalPosition, 64 * 1024);
        byte[] sample = new byte[sampleLength];
        read = stream.Read(sample, 0, sample.Length);
        stream.Position = originalPosition;
        if (IsValidUtf8(sample, read))
        {
            return new UTF8Encoding(false, true);
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(54936);
    }

    /// <summary>
    /// 验证指定字节序列是否符合 UTF-8 编码规则。
    /// </summary>
    /// <param name="bytes">待验证的字节数组。</param>
    /// <param name="length">参与验证的字节数。</param>
    /// <returns>字节序列有效时返回 true，否则返回 false。</returns>
    private static bool IsValidUtf8(byte[] bytes, int length)
    {
        try
        {
            new UTF8Encoding(false, true).GetString(bytes, 0, length);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    /// 查找 ZIP 中的指定条目，并忽略路径大小写差异。
    /// </summary>
    /// <param name="archive">要搜索的压缩包。</param>
    /// <param name="entryPath">条目路径。</param>
    /// <returns>找到的条目；不存在时返回 <see langword="null"/>。</returns>
    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryPath)
    {
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, entryPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 查找 ZIP 中的必要条目，不存在时抛出用户可读异常。
    /// </summary>
    /// <param name="archive">要搜索的压缩包。</param>
    /// <param name="entryPath">必要条目路径。</param>
    /// <returns>找到的 ZIP 条目。</returns>
    /// <exception cref="BookContentLoadException">必要条目不存在时抛出。</exception>
    private static ZipArchiveEntry GetRequiredEntry(ZipArchive archive, string entryPath)
    {
        return FindEntry(archive, entryPath)
            ?? throw new BookContentLoadException($"文档缺少必要文件：{entryPath}");
    }

    /// <summary>
    /// 在禁用外部实体的情况下读取 XML，避免文档引用本机资源。
    /// </summary>
    /// <param name="stream">包含 XML 的流。</param>
    /// <returns>已解析的 XML 文档。</returns>
    /// <exception cref="XmlException">XML 内容无效时抛出。</exception>
    private static XDocument LoadXml(Stream stream)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreComments = true
        };
        using XmlReader reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader);
    }
}
