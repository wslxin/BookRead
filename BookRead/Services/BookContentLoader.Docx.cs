using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using BookRead.Models;

namespace BookRead.Services;

/// <summary>
/// 提供 DOCX 文档的正文提取和章节生成逻辑。
/// </summary>
internal static partial class BookContentLoader
{
    /// <summary>
    /// 读取 DOCX 主文档，并按标题样式拆分章节。
    /// </summary>
    /// <param name="filePath">DOCX 文件路径。</param>
    /// <returns>解析后的章节列表。</returns>
    /// <exception cref="InvalidDataException">压缩包结构无效时抛出。</exception>
    /// <exception cref="BookContentLoadException">缺少必要文件或没有可读取正文时抛出。</exception>
    private static async Task<IReadOnlyList<BookChapter>> LoadDocxAsync(string filePath)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false);
        ZipArchiveEntry documentEntry = GetRequiredEntry(archive, "word/document.xml");

        using var memoryStream = new MemoryStream();
        await using (Stream documentStream = documentEntry.Open())
        {
            await documentStream.CopyToAsync(memoryStream);
        }

        memoryStream.Position = 0;
        XDocument document = LoadXml(memoryStream);
        IReadOnlyList<BookChapter> chapters = ParseDocxChapters(document);
        if (chapters.Count == 0)
        {
            throw new BookContentLoadException("DOCX 中没有可读取的正文。");
        }

        return chapters;
    }

    /// <summary>
    /// 按文档顺序读取段落，并将标题样式段落作为章节起点。
    /// </summary>
    /// <param name="document">已解析的 DOCX 主文档。</param>
    /// <returns>解析后的章节列表。</returns>
    private static IReadOnlyList<BookChapter> ParseDocxChapters(XDocument document)
    {
        var chapters = new List<BookChapter>();
        var prefaceContent = new StringBuilder();
        var currentContent = new StringBuilder();
        string? currentTitle = null;

        foreach (XElement paragraph in document.Descendants().Where(element => element.Name.LocalName == "p"))
        {
            string text = ExtractDocxParagraphText(paragraph);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (IsDocxHeadingParagraph(paragraph))
            {
                if (currentTitle is null)
                {
                    AddDocxPreface(chapters, prefaceContent);
                }
                else
                {
                    AddDocxChapter(chapters, currentTitle, currentContent);
                }

                currentTitle = text;
                currentContent.Clear();
                continue;
            }

            AppendDocxParagraph(currentTitle is null ? prefaceContent : currentContent, text);
        }

        if (currentTitle is null)
        {
            string content = prefaceContent.ToString().Trim();
            return content.Length == 0 ? [] : [new BookChapter("正文", content)];
        }

        AddDocxChapter(chapters, currentTitle, currentContent);
        return chapters;
    }

    /// <summary>
    /// 将首个标题前的正文作为前言加入章节列表。
    /// </summary>
    /// <param name="chapters">目标章节列表。</param>
    /// <param name="prefaceContent">标题前正文。</param>
    /// <returns>无。</returns>
    private static void AddDocxPreface(List<BookChapter> chapters, StringBuilder prefaceContent)
    {
        string content = prefaceContent.ToString().Trim();
        if (content.Length > 0)
        {
            chapters.Add(new BookChapter("前言", content));
        }
    }

    /// <summary>
    /// 将当前 DOCX 标题和正文加入章节列表。
    /// </summary>
    /// <param name="chapters">目标章节列表。</param>
    /// <param name="title">当前章节标题。</param>
    /// <param name="content">当前章节正文。</param>
    /// <returns>无。</returns>
    private static void AddDocxChapter(
        List<BookChapter> chapters,
        string title,
        StringBuilder content)
    {
        string normalizedContent = content.ToString().Trim();
        if (normalizedContent.Length == 0)
        {
            return;
        }

        string fallbackTitle = $"第 {chapters.Count + 1} 章";
        chapters.Add(new BookChapter(
            string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim(),
            normalizedContent));
    }

    /// <summary>
    /// 向段落缓冲区追加文本，并在段落之间保留空行。
    /// </summary>
    /// <param name="builder">段落缓冲区。</param>
    /// <param name="text">要追加的段落文本。</param>
    /// <returns>无。</returns>
    private static void AppendDocxParagraph(StringBuilder builder, string text)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine().AppendLine();
        }

        builder.Append(text);
    }

    /// <summary>
    /// 提取 DOCX 段落中的普通文本、制表符和换行符。
    /// </summary>
    /// <param name="paragraph">DOCX 段落元素。</param>
    /// <returns>段落中的纯文本。</returns>
    private static string ExtractDocxParagraphText(XElement paragraph)
    {
        var builder = new StringBuilder();
        foreach (XElement element in paragraph.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "t":
                    builder.Append(element.Value);
                    break;
                case "tab":
                    builder.Append('\t');
                    break;
                case "br":
                case "cr":
                    builder.AppendLine();
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// 判断 DOCX 段落是否使用 Heading、中文“标题”样式或大纲级别。
    /// </summary>
    /// <param name="paragraph">DOCX 段落元素。</param>
    /// <returns>段落应作为章节标题时返回 true，否则返回 false。</returns>
    private static bool IsDocxHeadingParagraph(XElement paragraph)
    {
        string? styleId = paragraph
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "pStyle")
            ?.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "val")
            ?.Value;
        if (styleId?.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) == true
            || styleId?.StartsWith("标题", StringComparison.Ordinal) == true)
        {
            return true;
        }

        // 部分文档只通过 outlineLvl 标记标题层级，因此将其作为样式识别的补充。
        string? outlineLevel = paragraph
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "outlineLvl")
            ?.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "val")
            ?.Value;
        return int.TryParse(outlineLevel, out int level) && level is >= 0 and <= 6;
    }
}
