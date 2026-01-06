// 制約事項:
// - Joplin 固有のすべての Markdown 拡張や HTML タグの完全互換は未検証。
// - updated/created のローカル時刻変換は実行環境のタイムゾーンに依存する。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SixLabors.ImageSharp;

public static class JoplinMdToNotesnookHtmlExporter
{
    /// <summary>
    /// Joplin の Markdown (Front Matter 付き) を Notesnook 用 HTML に変換する。
    /// </summary>
    /// <param name="srcMdFileNameFullPath">入力 Markdown ファイルのフルパス。</param>
    /// <param name="dstHtmlDirFullPath">出力 HTML を保存するディレクトリのフルパス。</param>
    /// <param name="warningsLogFilePath">警告ログを追記するファイルパス。</param>
    public static void Convert(string srcMdFileNameFullPath, string dstHtmlDirFullPath, string warningsLogFilePath)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            if (string.IsNullOrWhiteSpace(srcMdFileNameFullPath))
            {
                throw new ArgumentException("APPERROR: srcMdFileNameFullPath is empty.", nameof(srcMdFileNameFullPath));
            }

            if (string.IsNullOrWhiteSpace(dstHtmlDirFullPath))
            {
                throw new ArgumentException("APPERROR: dstHtmlDirFullPath is empty.", nameof(dstHtmlDirFullPath));
            }

            if (string.IsNullOrWhiteSpace(warningsLogFilePath))
            {
                throw new ArgumentException("APPERROR: warningsLogFilePath is empty.", nameof(warningsLogFilePath));
            }

            if (!File.Exists(srcMdFileNameFullPath))
            {
                throw new FileNotFoundException($"APPERROR: Markdown file not found. Path: {srcMdFileNameFullPath}");
            }

            if (!string.Equals(Path.GetExtension(srcMdFileNameFullPath), ".md", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("APPERROR: Source file extension is not .md.");
            }

            string markdown = ReadAllTextNormalized(srcMdFileNameFullPath);
            var warnings = new List<string>();

            FrontMatterInfo frontMatter = ParseFrontMatter(markdown, Path.GetFileName(srcMdFileNameFullPath), warnings, out string bodyMarkdown);

            string outputDir = Path.GetFullPath(dstHtmlDirFullPath);
            Directory.CreateDirectory(outputDir);

            string attachmentsDir = Path.Combine(outputDir, "attachments");
            Directory.CreateDirectory(attachmentsDir);

            string sourceDir = Path.GetDirectoryName(srcMdFileNameFullPath) ?? throw new InvalidOperationException("APPERROR: Source directory is not available.");
            string resourcesDir = Path.GetFullPath(Path.Combine(sourceDir, "..", "_resources"));

            var context = new RenderContext
            {
                SourceMarkdownPath = srcMdFileNameFullPath,
                SourceDirectory = sourceDir,
                OutputDirectory = outputDir,
                AttachmentsDirectory = attachmentsDir,
                ResourcesDirectoryFullPath = resourcesDir,
                Warnings = warnings,
            };

            var pipeline = CreateMarkdownPipeline();
            if (string.IsNullOrWhiteSpace(frontMatter.Title))
            {
                string fallbackFileName = Path.GetFileNameWithoutExtension(srcMdFileNameFullPath);
                frontMatter.Title = DetermineFallbackTitle(bodyMarkdown, pipeline, fallbackFileName);
            }

            var writer = new HtmlWriter();
            WriteHtmlHeader(writer, frontMatter);

            writer.WriteLine("<body>");
            writer.IncreaseIndent();
            RenderBodyByLine(bodyMarkdown, pipeline, writer, context);
            writer.DecreaseIndent();
            writer.WriteLine("</body>");
            writer.WriteBlankLine();
            writer.WriteLine("</html>");

            string dstHtmlPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(srcMdFileNameFullPath) + ".html");
            File.WriteAllText(dstHtmlPath, writer.ToString(), new UTF8Encoding(true));

            AppendWarningsLogIfNeeded(warningsLogFilePath, Path.GetFileName(srcMdFileNameFullPath), warnings);
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("APPERROR:", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }

            throw new InvalidOperationException($"APPERROR: Conversion failed. Detail: {ex}", ex);
        }
    }

    /// <summary>
    /// Markdown の Front Matter を解析して情報を取得し、本文部分を切り出す。
    /// </summary>
    /// <param name="markdown">入力 Markdown 全文。</param>
    /// <param name="srcFileName">元ファイル名。</param>
    /// <param name="warnings">警告リスト。</param>
    /// <param name="bodyMarkdown">本文部分。</param>
    /// <returns>Front Matter 情報。</returns>
    static FrontMatterInfo ParseFrontMatter(string markdown, string srcFileName, List<string> warnings, out string bodyMarkdown)
    {
        if (markdown.Length > 0 && markdown[0] == '\uFEFF')
        {
            markdown = markdown.TrimStart('\uFEFF');
        }

        string[] lines = NormalizeNewlines(markdown).Split('\n');

        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            throw new InvalidOperationException("APPERROR: Front matter header is missing.");
        }

        int endIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex < 0)
        {
            throw new InvalidOperationException("APPERROR: Front matter footer is missing.");
        }

        var info = new FrontMatterInfo();
        bool hasCreated = false;
        bool hasUpdated = false;

        for (int i = 1; i < endIndex; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                warnings.Add($"Front matter line {i + 1} is invalid: {line}");
                continue;
            }

            string key = line[..colonIndex].Trim().ToLowerInvariant();
            string value = line[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "title":
                    if (TryParseYamlBlockScalar(value, lines, ref i, endIndex, out string titleValue))
                    {
                        info.Title = titleValue;
                    }
                    else
                    {
                        info.Title = value;
                    }
                    break;
                case "created":
                    info.CreatedUtc = ParseUtcDateTime(value, "created", i + 1);
                    hasCreated = true;
                    break;
                case "updated":
                    info.UpdatedUtc = ParseUtcDateTime(value, "updated", i + 1);
                    hasUpdated = true;
                    break;
            }
        }

        if (!hasCreated)
        {
            throw new InvalidOperationException("APPERROR: Front matter 'created' is missing.");
        }

        if (!hasUpdated)
        {
            throw new InvalidOperationException("APPERROR: Front matter 'updated' is missing.");
        }

        bodyMarkdown = string.Join("\n", lines.Skip(endIndex + 1));
        return info;
    }

    /// <summary>
    /// YAML のブロックスカラーを解析する。
    /// </summary>
    /// <param name="value">キー行の値文字列。</param>
    /// <param name="lines">Front Matter 行配列。</param>
    /// <param name="lineIndex">現在行インデックス (消費した行で更新)。</param>
    /// <param name="endIndex">Front Matter の終了行インデックス。</param>
    /// <param name="parsedValue">解析結果。</param>
    /// <returns>ブロックスカラーとして解析した場合は true。</returns>
    static bool TryParseYamlBlockScalar(string value, string[] lines, ref int lineIndex, int endIndex, out string parsedValue)
    {
        parsedValue = string.Empty;
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        char style = trimmed[0];
        if (style != '>' && style != '|')
        {
            return false;
        }

        char? chomp = null;
        int explicitIndent = 0;

        for (int i = 1; i < trimmed.Length; i++)
        {
            char ch = trimmed[i];
            if (ch == '+' || ch == '-')
            {
                chomp = ch;
            }
            else if (ch >= '0' && ch <= '9')
            {
                explicitIndent = explicitIndent * 10 + (ch - '0');
            }
        }

        int indent = explicitIndent;
        int startLine = lineIndex + 1;

        if (indent <= 0)
        {
            for (int i = startLine; i < endIndex; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                indent = CountIndent(line);
                break;
            }
        }

        if (indent <= 0)
        {
            lineIndex = Math.Min(lineIndex, endIndex - 1);
            return true;
        }

        var contentLines = new List<string>();
        int current = startLine;

        for (; current < endIndex; current++)
        {
            string line = lines[current];
            if (line.Length == 0)
            {
                contentLines.Add(string.Empty);
                continue;
            }

            int lineIndent = CountIndent(line);
            if (lineIndent < indent)
            {
                break;
            }

            contentLines.Add(line.Substring(indent));
        }

        lineIndex = current - 1;
        parsedValue = BuildBlockScalarValue(contentLines, style, chomp).Trim();
        return true;
    }

    /// <summary>
    /// ブロックスカラーの値文字列を構築する。
    /// </summary>
    /// <param name="lines">値行の一覧。</param>
    /// <param name="style">スカラ形式。</param>
    /// <param name="chomp">チョンプ指定。</param>
    /// <returns>構築済み文字列。</returns>
    static string BuildBlockScalarValue(List<string> lines, char style, char? chomp)
    {
        var builder = new StringBuilder();

        if (style == '|')
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(lines[i]);
            }
        }
        else
        {
            bool previousWasEmpty = false;
            foreach (string line in lines)
            {
                if (line.Length == 0)
                {
                    builder.Append('\n');
                    previousWasEmpty = true;
                    continue;
                }

                if (builder.Length > 0 && !previousWasEmpty)
                {
                    builder.Append(' ');
                }

                builder.Append(line);
                previousWasEmpty = false;
            }
        }

        string result = builder.ToString();
        if (chomp != '+')
        {
            result = result.TrimEnd('\n', '\r');
        }

        return result;
    }

    /// <summary>
    /// 行の先頭インデント量を取得する。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <returns>インデント量。</returns>
    static int CountIndent(string line)
    {
        int count = 0;
        foreach (char ch in line)
        {
            if (ch == ' ')
            {
                count++;
                continue;
            }

            if (ch == '\t')
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }

    /// <summary>
    /// タイトルが空の場合の代替タイトルを決定する。
    /// </summary>
    /// <param name="bodyMarkdown">本文 Markdown。</param>
    /// <param name="pipeline">Markdown パイプライン。</param>
    /// <param name="fallbackFileName">最終フォールバックのファイル名。</param>
    /// <returns>代替タイトル文字列。</returns>
    static string DetermineFallbackTitle(string bodyMarkdown, MarkdownPipeline pipeline, string fallbackFileName)
    {
        if (!string.IsNullOrEmpty(bodyMarkdown))
        {
            string[] lines = NormalizeNewlines(bodyMarkdown).Split('\n');
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string candidate = RenderPlainTextFromMarkdownLine(line, pipeline).Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return fallbackFileName;
    }

    /// <summary>
    /// 1 行の Markdown を通常テキストとしてレンダリングする。
    /// </summary>
    /// <param name="line">Markdown の 1 行。</param>
    /// <param name="pipeline">Markdown パイプライン。</param>
    /// <returns>通常テキストとしての文字列。</returns>
    static string RenderPlainTextFromMarkdownLine(string line, MarkdownPipeline pipeline)
    {
        MarkdownDocument document = Markdown.Parse(line, pipeline);
        return ExtractPlainTextFromBlocks(document);
    }

    /// <summary>
    /// ブロック列から通常テキストを抽出する。
    /// </summary>
    /// <param name="blocks">ブロック列。</param>
    /// <returns>抽出した通常テキスト。</returns>
    static string ExtractPlainTextFromBlocks(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            string candidate = ExtractPlainTextFromBlock(block);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// ブロックから通常テキストを抽出する。
    /// </summary>
    /// <param name="block">対象ブロック。</param>
    /// <returns>抽出した通常テキスト。</returns>
    static string ExtractPlainTextFromBlock(Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                return GetInlinePlainTextForTitle(paragraph.Inline);
            case HeadingBlock heading:
                return GetInlinePlainTextForTitle(heading.Inline);
            case QuoteBlock quote:
                return ExtractPlainTextFromBlocks(quote);
            case ListBlock list:
                foreach (Block item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        string candidate = ExtractPlainTextFromBlocks(listItem);
                        if (!string.IsNullOrWhiteSpace(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                return string.Empty;
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// UTC 文字列を DateTimeOffset として解析する。
    /// </summary>
    /// <param name="value">UTC 文字列。</param>
    /// <param name="fieldName">フィールド名。</param>
    /// <param name="lineNumber">行番号。</param>
    /// <returns>解析結果。</returns>
    static DateTimeOffset ParseUtcDateTime(string value, string fieldName, int lineNumber)
    {
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset result))
        {
            throw new InvalidOperationException($"APPERROR: Front matter '{fieldName}' is invalid at line {lineNumber}: {value}");
        }

        return result;
    }

    /// <summary>
    /// Markdown 用パイプラインを構築する。
    /// </summary>
    /// <returns>構築したパイプライン。</returns>
    static MarkdownPipeline CreateMarkdownPipeline()
    {
        return new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseTaskLists()
            .UseAutoLinks()
            .Build();
    }

    /// <summary>
    /// HTML ヘッダ部を書き出す。
    /// </summary>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="frontMatter">Front Matter 情報。</param>
    static void WriteHtmlHeader(HtmlWriter writer, FrontMatterInfo frontMatter)
    {
        writer.WriteLine("<!DOCTYPE html>");
        writer.WriteLine("<html lang=\"en-US\">");
        writer.WriteBlankLine();
        writer.WriteLine("<head>");
        writer.IncreaseIndent();
        writer.WriteLine("<meta charset=\"utf-8\" />");
        writer.WriteLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=Edge,chrome=1\" />");
        writer.WriteLine($"<title>{HtmlUtility.EscapeText(frontMatter.Title)}</title>");
        writer.WriteLine($"<meta name=\"created-at\" content=\"{HtmlUtility.EscapeAttribute(FormatNotesnookDate(frontMatter.CreatedUtc))}\" />");
        writer.WriteLine($"<meta name=\"updated-at\" content=\"{HtmlUtility.EscapeAttribute(FormatNotesnookDate(frontMatter.UpdatedUtc))}\" />");
        writer.WriteLine("<link rel=\"stylesheet\" href=\"https://app.notesnook.com/assets/editor-styles.css?d=1690887574068\">");
        writer.DecreaseIndent();
        writer.WriteLine("</head>");
        writer.WriteBlankLine();
    }

    /// <summary>
    /// Notesnook 向けの日時表記に変換する。
    /// </summary>
    /// <param name="utcDateTime">UTC 日時。</param>
    /// <returns>フォーマット済み文字列。</returns>
    static string FormatNotesnookDate(DateTimeOffset utcDateTime)
    {
        return utcDateTime.ToLocalTime().ToString("MM-dd-yyyy hh:mm tt", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// ブロック要素を HTML に変換して出力する。
    /// </summary>
    /// <param name="blocks">ブロック列。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderBlocks(IEnumerable<Block> blocks, HtmlWriter writer, RenderContext context)
    {
        foreach (Block block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    RenderParagraphBlock(paragraph, writer, context);
                    break;
                case HeadingBlock heading:
                    RenderHeadingBlock(heading, writer, context);
                    break;
                case ListBlock list:
                    RenderListBlock(list, writer, context);
                    break;
                case FencedCodeBlock fencedCode:
                    RenderCodeBlock(fencedCode, writer);
                    break;
                case CodeBlock codeBlock:
                    RenderCodeBlock(codeBlock, writer);
                    break;
                case ThematicBreakBlock:
                    writer.WriteLine("<hr>");
                    break;
                case QuoteBlock quote:
                    RenderQuoteBlock(quote, writer, context);
                    break;
                case HtmlBlock htmlBlock:
                    RenderHtmlBlock(htmlBlock, writer, context);
                    break;
                case LinkReferenceDefinitionGroup:
                    // 参照リンク定義は本文出力の対象外とする。
                    break;
                case BlankLineBlock:
                    break;
                default:
                    RenderUnsupportedBlock(block, writer, context);
                    break;
            }
        }
    }

    /// <summary>
    /// 本文を行ごとに処理して HTML 出力する。
    /// </summary>
    /// <param name="bodyMarkdown">本文 Markdown。</param>
    /// <param name="pipeline">Markdown パイプライン。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderBodyByLine(string bodyMarkdown, MarkdownPipeline pipeline, HtmlWriter writer, RenderContext context)
    {
        string[] lines = NormalizeNewlines(bodyMarkdown).Split('\n');
        foreach (string line in lines)
        {
            string trimmedLine = line.TrimStart();
            if (TryRenderHtmlAnchorLine(trimmedLine, writer, context))
            {
                continue;
            }

            if (TryRenderHtmlImageLine(trimmedLine, writer, context))
            {
                continue;
            }

            MarkdownDocument lineDocument = Markdown.Parse(line, pipeline);
            if (IsMarkdownDocument(lineDocument))
            {
                RenderBlocks(lineDocument, writer, context);
                continue;
            }

            string html = RenderPlainTextLineWithAutoLinks(line);
            if (string.IsNullOrEmpty(html))
            {
                html = "&nbsp;";
            }

            writer.WriteLine($"<p data-spacing=\"single\">{html}</p>");
        }
    }

    /// <summary>
    /// Markdown として処理すべき本文かどうかを判定する。
    /// </summary>
    /// <param name="document">Markdown 文書。</param>
    /// <returns>Markdown なら true。</returns>
    static bool IsMarkdownDocument(MarkdownDocument document)
    {
        foreach (Block block in document)
        {
            if (ContainsMarkdownIndicatorBlock(block))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ブロック内に Markdown の判定要素が含まれるかを判定する。
    /// </summary>
    /// <param name="block">対象ブロック。</param>
    /// <returns>判定要素があれば true。</returns>
    static bool ContainsMarkdownIndicatorBlock(Block block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                return ContainsMarkdownInline(paragraph.Inline);
            case HeadingBlock heading:
                return ContainsMarkdownInline(heading.Inline);
            case QuoteBlock quote:
                return ContainsMarkdownIndicatorBlocks(quote);
            case ListBlock list:
                return ContainsMarkdownIndicatorBlocks(list);
            case ListItemBlock listItem:
                return ContainsMarkdownIndicatorBlocks(listItem);
            default:
                return false;
        }
    }

    /// <summary>
    /// ブロック列内に Markdown の判定要素が含まれるかを判定する。
    /// </summary>
    /// <param name="blocks">対象ブロック列。</param>
    /// <returns>判定要素があれば true。</returns>
    static bool ContainsMarkdownIndicatorBlocks(IEnumerable<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            if (ContainsMarkdownIndicatorBlock(block))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// インラインに Markdown の判定要素が含まれるかを判定する。
    /// </summary>
    /// <param name="inline">インライン列。</param>
    /// <returns>判定要素があれば true。</returns>
    static bool ContainsMarkdownInline(Inline? inline)
    {
        for (Inline? current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LinkInline linkInline:
                    if (linkInline.IsImage)
                    {
                        return true;
                    }

                    if (linkInline.IsAutoLink)
                    {
                        break;
                    }

                    string url = linkInline.Url ?? string.Empty;
                    string title = linkInline.Title ?? string.Empty;

                    if (string.Equals(url, "#", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(title))
                    {
                        string normalizedTitle = title.Trim();
                        if (normalizedTitle.Length >= 2)
                        {
                            char first = normalizedTitle[0];
                            char last = normalizedTitle[^1];
                            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                            {
                                normalizedTitle = normalizedTitle[1..^1];
                            }
                        }

                        if (normalizedTitle.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                        {
                            url = normalizedTitle;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        break;
                    }

                    if (IsXSchemeLinkDestination(url) || IsNumericOnlyLinkDestination(url) || IsMaxLinkDestination(url))
                    {
                        break;
                    }

                    return true;
                case ContainerInline container:
                    if (ContainsMarkdownInline(container.FirstChild))
                    {
                        return true;
                    }

                    break;
                default:
                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// 段落ブロックを HTML に変換して出力する。
    /// </summary>
    /// <param name="paragraph">段落ブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderParagraphBlock(ParagraphBlock paragraph, HtmlWriter writer, RenderContext context)
    {
        var inlineWriter = new SegmentedInlineWriter();

        // ★ 複数行を data-spacing で分割するため、行区切りで段落を分割する
        RenderInlineContent(paragraph.Inline, context, inlineWriter);
        inlineWriter.Finish();

        bool isFirstSegment = true;
        foreach (InlineSegment segment in inlineWriter.Segments)
        {
            if (!segment.HasVisibleContent)
            {
                continue;
            }

            bool isImageOnly = segment.HasImage && !segment.HasNonImageContent;
            string spacing = isImageOnly ? "single" : (isFirstSegment ? "double" : "single");

            writer.WriteLine($"<p data-spacing=\"{spacing}\">{segment.Content}</p>");
            isFirstSegment = false;
        }
    }

    /// <summary>
    /// プレーンテキスト行を自動リンク付きで HTML 化する。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <returns>HTML 文字列。</returns>
    static string RenderPlainTextLineWithAutoLinks(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        int index = 0;

        while (index < line.Length)
        {
            if (!TryFindAutoLinkStart(line, index, out int linkStart, out int schemeLength))
            {
                builder.Append(HtmlUtility.EscapeTextPreserveSpaces(line[index..]));
                break;
            }

            if (linkStart > index)
            {
                builder.Append(HtmlUtility.EscapeTextPreserveSpaces(line.Substring(index, linkStart - index)));
            }

            string remainder = line[linkStart..];
            int endIndex = FindAutoLinkEndIndex(remainder);
            int trimmedEnd = TrimAutoLinkTrailingPunctuation(remainder, endIndex);
            if (trimmedEnd <= schemeLength)
            {
                builder.Append(HtmlUtility.EscapeTextPreserveSpaces(remainder.Substring(0, schemeLength)));
                index = linkStart + schemeLength;
                continue;
            }

            string url = remainder.Substring(0, trimmedEnd);
            builder.Append(BuildAutoLinkHtml(url));

            index = linkStart + trimmedEnd;
        }

        return builder.ToString();
    }

    /// <summary>
    /// HTML の <a> タグ行をレンダリングする。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>処理した場合は true。</returns>
    static bool TryRenderHtmlAnchorLine(string line, HtmlWriter writer, RenderContext context)
    {
        if (!IsHtmlTagLineStart(line, "a"))
        {
            return false;
        }

        if (!TryGetHtmlAttributeValue(line, "href", out string rawHref))
        {
            return false;
        }

        string href = DecodeHtmlValue(rawHref);
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        TryExtractHtmlInnerText(line, "a", out string innerText, out string trailingText);

        string label = DecodeHtmlValue(innerText);
        string trailing = DecodeHtmlValue(trailingText);

        string linkHtml;
        if (IsWebLink(href))
        {
            string displayText = string.IsNullOrWhiteSpace(label) ? href : label;
            string escapedText = HtmlUtility.EscapeTextPreserveSpaces(displayText);
            string escapedHref = HtmlUtility.EscapeAttribute(href);
            linkHtml = $"<a target=\"_blank\" rel=\"noopener noreferrer nofollow\" spellcheck=\"false\" href=\"{escapedHref}\" title=\"{escapedHref}\">{escapedText}</a>";
        }
        else
        {
            AttachmentInfo attachment = ResolveAttachmentInfo(href, context);
            string displayText = string.IsNullOrWhiteSpace(label) ? attachment.OutputFileName : label;
            string escapedText = HtmlUtility.EscapeTextPreserveSpaces(displayText);
            string hrefValue = $"./attachments/{attachment.OutputFileName}";
            linkHtml = $"<a href=\"{HtmlUtility.EscapeAttribute(hrefValue)}\" title=\"{HtmlUtility.EscapeAttribute(displayText)}\">{escapedText}</a>";
        }

        string trailingHtml = string.IsNullOrEmpty(trailing) ? string.Empty : HtmlUtility.EscapeTextPreserveSpaces(trailing);
        writer.WriteLine($"<p data-spacing=\"double\">{linkHtml}{trailingHtml}</p>");
        return true;
    }

    /// <summary>
    /// HTML の <img> タグ行をレンダリングする。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>処理した場合は true。</returns>
    static bool TryRenderHtmlImageLine(string line, HtmlWriter writer, RenderContext context)
    {
        if (!IsHtmlTagLineStart(line, "img"))
        {
            return false;
        }

        if (!TryGetHtmlAttributeValue(line, "src", out string rawSrc))
        {
            return false;
        }

        string src = DecodeHtmlValue(rawSrc);
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        string imageHtml = BuildImageHtml(src, context);
        string trailingText = ExtractTrailingTextAfterTag(line);
        string trailing = DecodeHtmlValue(trailingText);
        string trailingHtml = string.IsNullOrEmpty(trailing) ? string.Empty : HtmlUtility.EscapeTextPreserveSpaces(trailing);

        writer.WriteLine($"<p data-spacing=\"single\">{imageHtml}{trailingHtml}</p>");
        return true;
    }

    /// <summary>
    /// HTML タグ行かどうかを判定する。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="tagName">タグ名。</param>
    /// <returns>タグ行なら true。</returns>
    static bool IsHtmlTagLineStart(string line, string tagName)
    {
        string prefix = "<" + tagName;
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int index = prefix.Length;
        if (line.Length <= index)
        {
            return true;
        }

        char next = line[index];
        return char.IsWhiteSpace(next) || next == '>' || next == '/';
    }

    /// <summary>
    /// HTML タグの属性値を取得する。
    /// </summary>
    /// <param name="tagLine">タグ行。</param>
    /// <param name="attributeName">属性名。</param>
    /// <param name="value">取得した属性値。</param>
    /// <returns>取得できた場合は true。</returns>
    static bool TryGetHtmlAttributeValue(string tagLine, string attributeName, out string value)
    {
        value = string.Empty;
        int index = 0;

        while (index < tagLine.Length)
        {
            int found = tagLine.IndexOf(attributeName, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                return false;
            }

            int afterName = found + attributeName.Length;
            if (afterName < tagLine.Length && (char.IsLetterOrDigit(tagLine[afterName]) || tagLine[afterName] == '-' || tagLine[afterName] == '_'))
            {
                index = afterName;
                continue;
            }

            int pos = afterName;
            while (pos < tagLine.Length && char.IsWhiteSpace(tagLine[pos]))
            {
                pos++;
            }

            if (pos >= tagLine.Length || tagLine[pos] != '=')
            {
                index = afterName;
                continue;
            }

            pos++;
            while (pos < tagLine.Length && char.IsWhiteSpace(tagLine[pos]))
            {
                pos++;
            }

            if (pos >= tagLine.Length)
            {
                return false;
            }

            char quote = tagLine[pos];
            if (quote == '"' || quote == '\'')
            {
                pos++;
                int endQuote = tagLine.IndexOf(quote, pos);
                if (endQuote < 0)
                {
                    return false;
                }

                value = tagLine.Substring(pos, endQuote - pos);
                return true;
            }

            int end = pos;
            while (end < tagLine.Length && !char.IsWhiteSpace(tagLine[end]) && tagLine[end] != '>')
            {
                end++;
            }

            value = tagLine.Substring(pos, end - pos);
            return true;
        }

        return false;
    }

    /// <summary>
    /// HTML タグの内側テキストと末尾テキストを抽出する。
    /// </summary>
    /// <param name="tagLine">タグ行。</param>
    /// <param name="tagName">タグ名。</param>
    /// <param name="innerText">内側テキスト。</param>
    /// <param name="trailingText">末尾テキスト。</param>
    static void TryExtractHtmlInnerText(string tagLine, string tagName, out string innerText, out string trailingText)
    {
        innerText = string.Empty;
        trailingText = string.Empty;

        int openEnd = tagLine.IndexOf('>');
        if (openEnd < 0)
        {
            return;
        }

        string closeTag = "</" + tagName;
        int closeStart = tagLine.IndexOf(closeTag, openEnd + 1, StringComparison.OrdinalIgnoreCase);
        if (closeStart < 0)
        {
            innerText = tagLine[(openEnd + 1)..];
            return;
        }

        innerText = tagLine.Substring(openEnd + 1, closeStart - openEnd - 1);
        int closeEnd = tagLine.IndexOf('>', closeStart);
        if (closeEnd >= 0 && closeEnd + 1 < tagLine.Length)
        {
            trailingText = tagLine[(closeEnd + 1)..];
        }
    }

    /// <summary>
    /// HTML タグ行の末尾テキストを抽出する。
    /// </summary>
    /// <param name="tagLine">タグ行。</param>
    /// <returns>末尾テキスト。</returns>
    static string ExtractTrailingTextAfterTag(string tagLine)
    {
        int endIndex = tagLine.IndexOf('>');
        if (endIndex < 0 || endIndex + 1 >= tagLine.Length)
        {
            return string.Empty;
        }

        return tagLine[(endIndex + 1)..];
    }

    /// <summary>
    /// HTML エンティティをデコードする。
    /// </summary>
    /// <param name="value">入力文字列。</param>
    /// <returns>デコード後の文字列。</returns>
    static string DecodeHtmlValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return WebUtility.HtmlDecode(value);
    }

    /// <summary>
    /// 自動リンクの開始位置を検索する。
    /// </summary>
    /// <param name="line">対象行。</param>
    /// <param name="startIndex">検索開始位置。</param>
    /// <param name="linkStart">リンク開始位置。</param>
    /// <param name="schemeLength">スキーム長。</param>
    /// <returns>見つかった場合は true。</returns>
    static bool TryFindAutoLinkStart(string line, int startIndex, out int linkStart, out int schemeLength)
    {
        linkStart = -1;
        schemeLength = 0;

        string[] schemes = new[] { "http://", "https://", "ftp://", "mailto:", "tel:", "file://" };
        int bestIndex = -1;
        int bestLength = 0;

        foreach (string scheme in schemes)
        {
            int found = line.IndexOf(scheme, startIndex, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                continue;
            }

            if (bestIndex < 0 || found < bestIndex)
            {
                bestIndex = found;
                bestLength = scheme.Length;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        linkStart = bestIndex;
        schemeLength = bestLength;
        return true;
    }

    /// <summary>
    /// 自動リンク HTML を構築する。
    /// </summary>
    /// <param name="url">リンク先 URL。</param>
    /// <returns>HTML 文字列。</returns>
    static string BuildAutoLinkHtml(string url)
    {
        string escapedUrl = HtmlUtility.EscapeAttribute(url);
        string escapedText = HtmlUtility.EscapeText(url);
        return $"<a target=\"_blank\" rel=\"noopener noreferrer nofollow\" spellcheck=\"false\" href=\"{escapedUrl}\" title=\"{escapedUrl}\">{escapedText}</a>";
    }

    /// <summary>
    /// 見出しブロックを HTML に変換して出力する。
    /// </summary>
    /// <param name="heading">見出しブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderHeadingBlock(HeadingBlock heading, HtmlWriter writer, RenderContext context)
    {
        string content = RenderInlineToString(heading.Inline, context);
        writer.WriteLine($"<h{heading.Level}>{content}</h{heading.Level}>");
    }

    /// <summary>
    /// リストブロックを HTML に変換して出力する。
    /// </summary>
    /// <param name="list">リストブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderListBlock(ListBlock list, HtmlWriter writer, RenderContext context)
    {
        bool isTaskList = IsTaskList(list);
        string openTag = isTaskList ? "<ul class=\"simple-checklist\">" : (list.IsOrdered ? "<ol>" : "<ul>");
        string closeTag = isTaskList ? "</ul>" : (list.IsOrdered ? "</ol>" : "</ul>");

        writer.WriteLine(openTag);
        writer.IncreaseIndent();

        foreach (Block item in list)
        {
            if (item is ListItemBlock listItem)
            {
                RenderListItemBlock(listItem, writer, context, isTaskList);
            }
        }

        writer.DecreaseIndent();
        writer.WriteLine(closeTag);
    }

    /// <summary>
    /// リスト項目を HTML に変換して出力する。
    /// </summary>
    /// <param name="listItem">リスト項目。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <param name="isTaskList">タスクリストかどうか。</param>
    static void RenderListItemBlock(ListItemBlock listItem, HtmlWriter writer, RenderContext context, bool isTaskList)
    {
        string classAttribute = string.Empty;
        if (isTaskList)
        {
            bool isChecked = TryGetTaskListChecked(listItem, out bool checkedValue) && checkedValue;
            classAttribute = isChecked ? " class=\"checked simple-checklist--item\"" : " class=\"simple-checklist--item\"";
        }

        writer.WriteLine($"<li{classAttribute}>");
        writer.IncreaseIndent();
        RenderBlocks(listItem, writer, context);
        writer.DecreaseIndent();
        writer.WriteLine("</li>");
    }

    /// <summary>
    /// タスクリストかどうかを判定する。
    /// </summary>
    /// <param name="list">対象リスト。</param>
    /// <returns>タスクリストなら true。</returns>
    static bool IsTaskList(ListBlock list)
    {
        foreach (Block item in list)
        {
            if (item is ListItemBlock listItem && TryGetTaskListChecked(listItem, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// リスト項目に含まれるタスク状態を抽出する。
    /// </summary>
    /// <param name="listItem">リスト項目。</param>
    /// <param name="isChecked">チェック状態。</param>
    /// <returns>タスクリスト項目なら true。</returns>
    static bool TryGetTaskListChecked(ListItemBlock listItem, out bool isChecked)
    {
        foreach (Block block in listItem)
        {
            if (block is ParagraphBlock paragraph && TryGetTaskListInline(paragraph.Inline, out bool checkedValue))
            {
                isChecked = checkedValue;
                return true;
            }
        }

        isChecked = false;
        return false;
    }

    /// <summary>
    /// インラインからタスクリスト情報を探す。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <param name="isChecked">チェック状態。</param>
    /// <returns>タスクリスト情報があれば true。</returns>
    static bool TryGetTaskListInline(Inline? inline, out bool isChecked)
    {
        for (Inline? current = inline; current != null; current = current.NextSibling)
        {
            if (current is TaskList taskList)
            {
                isChecked = taskList.Checked;
                return true;
            }

            if (current is ContainerInline container && TryGetTaskListInline(container.FirstChild, out isChecked))
            {
                return true;
            }
        }

        isChecked = false;
        return false;
    }

    /// <summary>
    /// コードブロックを HTML に変換して出力する。
    /// </summary>
    /// <param name="codeBlock">コードブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    static void RenderCodeBlock(CodeBlock codeBlock, HtmlWriter writer)
    {
        string code = NormalizeNewlines(codeBlock.Lines.ToString() ?? string.Empty).TrimEnd('\n');
        string escapedCode = HtmlUtility.EscapeCodeText(code);
        writer.WriteLine($"<pre data-indent-type=\"space\" data-indent-length=\"2\"><code>{escapedCode}</code></pre>");
    }

    /// <summary>
    /// 引用ブロックを HTML に変換して出力する。
    /// </summary>
    /// <param name="quote">引用ブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderQuoteBlock(QuoteBlock quote, HtmlWriter writer, RenderContext context)
    {
        writer.WriteLine("<blockquote>");
        writer.IncreaseIndent();
        RenderBlocks(quote, writer, context);
        writer.DecreaseIndent();
        writer.WriteLine("</blockquote>");
    }

    /// <summary>
    /// HTML ブロックを処理する。
    /// </summary>
    /// <param name="htmlBlock">HTML ブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderHtmlBlock(HtmlBlock htmlBlock, HtmlWriter writer, RenderContext context)
    {
        string rawHtml = NormalizeNewlines(htmlBlock.Lines.ToString() ?? string.Empty).Trim();

        if (IsLineBreakHtmlTag(rawHtml))
        {
            writer.WriteLine("<p data-spacing=\"single\">&nbsp;</p>");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawHtml))
        {
            return;
        }

        context.Warnings.Add($"HTML block is output as-is: {rawHtml}");
        foreach (string line in rawHtml.Split('\n'))
        {
            writer.WriteLine(line);
        }
    }

    /// <summary>
    /// 未対応ブロックを警告しつつ出力する。
    /// </summary>
    /// <param name="block">対象ブロック。</param>
    /// <param name="writer">HTML 出力ライター。</param>
    /// <param name="context">変換コンテキスト。</param>
    static void RenderUnsupportedBlock(Block block, HtmlWriter writer, RenderContext context)
    {
        context.Warnings.Add($"Unsupported block '{block.GetType().Name}' is rendered as plain text.");

        string rawText = block is LeafBlock leafBlock
            ? NormalizeNewlines(leafBlock.Lines.ToString() ?? string.Empty)
            : NormalizeNewlines(block.ToString() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return;
        }

        writer.WriteLine($"<p data-spacing=\"double\">{HtmlUtility.EscapeText(rawText)}</p>");
    }

    /// <summary>
    /// インライン要素を HTML に変換して書き出す。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <param name="writer">インライン出力ライター。</param>
    static void RenderInlineContent(Inline? inline, RenderContext context, IInlineWriter writer)
    {
        for (Inline? current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    writer.WriteText(literal.Content.ToString(), false);
                    break;
                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard)
                    {
                        writer.LineBreak();
                    }
                    else
                    {
                        writer.WriteText(" ", false);
                    }
                    break;
                case CodeInline codeInline:
                    writer.OpenTag("code", "spellcheck=\"false\"");
                    writer.WriteText(codeInline.Content, true);
                    writer.CloseTag("code");
                    break;
                case EmphasisInline emphasis:
                    string tagName = GetEmphasisTagName(emphasis);
                    writer.OpenTag(tagName);
                    RenderInlineContent(emphasis.FirstChild, context, writer);
                    writer.CloseTag(tagName);
                    break;
                case LinkInline linkInline:
                    RenderLinkInline(linkInline, context, writer);
                    break;
                case HtmlInline htmlInline:
                    RenderHtmlInline(htmlInline, context, writer);
                    break;
                case HtmlEntityInline htmlEntity:
                    string entityText = htmlEntity.Transcoded.ToString();
                    if (string.IsNullOrEmpty(entityText))
                    {
                        entityText = htmlEntity.Original.ToString();
                    }

                    if (!string.IsNullOrEmpty(entityText))
                    {
                        if (entityText.StartsWith("&", StringComparison.Ordinal) && entityText.EndsWith(";", StringComparison.Ordinal))
                        {
                            writer.WriteRawHtml(entityText, true);
                        }
                        else
                        {
                            writer.WriteText(entityText, true);
                        }
                    }
                    break;
                case TaskList:
                    break;
                case ContainerInline container:
                    RenderInlineContent(container.FirstChild, context, writer);
                    break;
                default:
                    context.Warnings.Add($"Unsupported inline '{current.GetType().Name}' is rendered as text.");
                    writer.WriteText(current.ToString() ?? string.Empty, true);
                    break;
            }
        }
    }

    /// <summary>
    /// 見出し等のためにインラインを単一文字列に変換する。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>HTML 文字列。</returns>
    static string RenderInlineToString(Inline? inline, RenderContext context)
    {
        var writer = new InlineStringWriter();
        RenderInlineContent(inline, context, writer);
        return writer.ToString();
    }

    /// <summary>
    /// Emphasis のタグ名を決定する。
    /// </summary>
    /// <param name="emphasis">Emphasis インライン。</param>
    /// <returns>タグ名。</returns>
    static string GetEmphasisTagName(EmphasisInline emphasis)
    {
        if (emphasis.DelimiterChar == '~')
        {
            return "s";
        }

        return emphasis.DelimiterCount >= 2 ? "strong" : "em";
    }

    /// <summary>
    /// リンクや添付ファイルを処理する。
    /// </summary>
    /// <param name="linkInline">リンクインライン。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <param name="writer">インライン出力ライター。</param>
    static void RenderLinkInline(LinkInline linkInline, RenderContext context, IInlineWriter writer)
    {
        string url = linkInline.Url ?? string.Empty;
        string title = linkInline.Title ?? string.Empty;

        // ★ Joplin 由来で (# "tel:...") の形式があり得るため、title をリンク先として扱う
        if (string.Equals(url, "#", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(title))
        {
            string normalizedTitle = title.Trim();
            if (normalizedTitle.Length >= 2)
            {
                char first = normalizedTitle[0];
                char last = normalizedTitle[^1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                {
                    normalizedTitle = normalizedTitle[1..^1];
                }
            }

            if (normalizedTitle.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            {
                url = normalizedTitle;
            }
        }

        if (linkInline.IsImage)
        {
            string imageHtml = BuildImageHtml(url, context);
            writer.WriteImageHtml(imageHtml);
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            context.Warnings.Add("Link URL is empty.");
            return;
        }

        if (IsXSchemeLinkDestination(url))
        {
            string label = GetInlinePlainText(linkInline.FirstChild);
            string xSchemeDisplayText = string.IsNullOrWhiteSpace(label) ? url : label;
            writer.WriteText(xSchemeDisplayText, true);
            return;
        }

        if (IsNumericOnlyLinkDestination(url) || IsMaxLinkDestination(url))
        {
            string label = GetInlinePlainText(linkInline.FirstChild);
            string numericDisplayText = $"[{label}]({url})";
            writer.WriteText(numericDisplayText, true);
            return;
        }

        string? autoLinkTrailingText = null;
        if (linkInline.IsAutoLink && TrySplitAutoLinkUrl(url, out string trimmedUrl, out string trailingText))
        {
            url = trimmedUrl;
            autoLinkTrailingText = trailingText;
        }

        if (IsWebLink(url))
        {
            string label = GetInlinePlainText(linkInline.FirstChild);
            if (string.IsNullOrWhiteSpace(label) || linkInline.IsAutoLink)
            {
                label = url;
            }

            string linkAttributes = $"target=\"_blank\" rel=\"noopener noreferrer nofollow\" spellcheck=\"false\" href=\"{HtmlUtility.EscapeAttribute(url)}\" title=\"{HtmlUtility.EscapeAttribute(url)}\"";
            writer.OpenTag("a", linkAttributes);
            writer.WriteText(label, true);
            writer.CloseTag("a");
            if (!string.IsNullOrEmpty(autoLinkTrailingText))
            {
                writer.WriteText(autoLinkTrailingText, true);
            }
            return;
        }

        AttachmentInfo attachment = ResolveAttachmentInfo(url, context);
        string displayText = GetInlinePlainText(linkInline.FirstChild);
        if (string.IsNullOrWhiteSpace(displayText))
        {
            displayText = attachment.OutputFileName;
        }

        string href = $"./attachments/{attachment.OutputFileName}";
        string attachmentAttributes = $"href=\"{HtmlUtility.EscapeAttribute(href)}\" title=\"{HtmlUtility.EscapeAttribute(displayText)}\"";
        writer.OpenTag("a", attachmentAttributes);
        writer.WriteText(displayText, true);
        writer.CloseTag("a");
    }

    /// <summary>
    /// HTML インラインを処理する。
    /// </summary>
    /// <param name="htmlInline">HTML インライン。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <param name="writer">インライン出力ライター。</param>
    static void RenderHtmlInline(HtmlInline htmlInline, RenderContext context, IInlineWriter writer)
    {
        string tag = htmlInline.Tag ?? string.Empty;
        if (IsLineBreakHtmlTag(tag))
        {
            writer.LineBreak();
            return;
        }

        if (IsUnderlineStartTag(tag))
        {
            writer.OpenTag("u");
            return;
        }

        if (IsUnderlineEndTag(tag))
        {
            writer.CloseTag("u");
            return;
        }

        context.Warnings.Add($"HTML inline is output as-is: {tag}");
        writer.WriteRawHtml(tag, true);
    }

    /// <summary>
    /// 画像タグ用 HTML を生成する。
    /// </summary>
    /// <param name="url">画像の URL (相対パス)。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>HTML 文字列。</returns>
    static string BuildImageHtml(string url, RenderContext context)
    {
        if (IsHttpImageUrl(url))
        {
            string escapedUrl = HtmlUtility.EscapeAttribute(url);
            return $"<span class=\"image-container\" alt=\"{escapedUrl}\"><img class=\"\" src=\"{escapedUrl}\" alt=\"{escapedUrl}\"></span>";
        }

        ImageInfo imageInfo = ResolveImageInfo(url, context);
        string href = $"./attachments/{imageInfo.OutputFileName}";
        string alt = imageInfo.OutputFileName;

        string escapedHref = HtmlUtility.EscapeAttribute(href);
        string escapedAlt = HtmlUtility.EscapeAttribute(alt);

        if (imageInfo.HasSize)
        {
            return $"<span class=\"image-container\" alt=\"{escapedAlt}\"><img class=\"\" src=\"{escapedHref}\" alt=\"{escapedAlt}\" width=\"{imageInfo.Width}\" height=\"{imageInfo.Height}\"></span>";
        }

        return $"<span class=\"image-container\" alt=\"{escapedAlt}\"><img class=\"\" src=\"{escapedHref}\" alt=\"{escapedAlt}\"></span>";
    }

    /// <summary>
    /// 添付ファイル情報を解決してコピーする。
    /// </summary>
    /// <param name="url">Markdown 内の URL。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>添付ファイル情報。</returns>
    static AttachmentInfo ResolveAttachmentInfo(string url, RenderContext context)
    {
        string fullPath = ResolveResourcePath(url, context);

        if (context.AttachmentNameBySourcePath.TryGetValue(fullPath, out string? outputName))
        {
            return new AttachmentInfo
            {
                SourceFullPath = fullPath,
                OutputFileName = outputName,
            };
        }

        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        string fileName;
        string dstPath;

        do
        {
            string randomHex = GenerateRandomHex32();
            fileName = string.IsNullOrEmpty(extension) ? $"attachment_{randomHex}" : $"attachment_{randomHex}{extension}";
            dstPath = Path.Combine(context.AttachmentsDirectory, fileName);
        }
        while (File.Exists(dstPath));

        File.Copy(fullPath, dstPath, overwrite: false);
        context.AttachmentNameBySourcePath[fullPath] = fileName;

        return new AttachmentInfo
        {
            SourceFullPath = fullPath,
            OutputFileName = fileName,
        };
    }

    /// <summary>
    /// 画像情報を解決してコピーし、サイズを取得する。
    /// </summary>
    /// <param name="url">Markdown 内の URL。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>画像情報。</returns>
    static ImageInfo ResolveImageInfo(string url, RenderContext context)
    {
        string fullPath = ResolveResourcePath(url, context);

        if (context.ImageInfoBySourcePath.TryGetValue(fullPath, out ImageInfo? cached))
        {
            return cached;
        }

        string outputFileName = Path.GetFileName(fullPath).ToLowerInvariant();
        string dstPath = Path.Combine(context.AttachmentsDirectory, outputFileName);

        int width = 0;
        int height = 0;
        bool hasSize = false;

        try
        {
            using (Image image = Image.Load(fullPath))
            {
                width = image.Width;
                height = image.Height;
                hasSize = true;
            }
        }
        catch (Exception ex)
        {
            context.Warnings.Add($"ImageSharp failed to read image size. Path: {fullPath}. Detail: {ex}");
        }

        File.Copy(fullPath, dstPath, overwrite: true);

        var info = new ImageInfo
        {
            SourceFullPath = fullPath,
            OutputFileName = outputFileName,
            Width = width,
            Height = height,
            HasSize = hasSize,
        };

        context.ImageInfoBySourcePath[fullPath] = info;
        return info;
    }

    /// <summary>
    /// リソースパスを解決し、存在チェックと制約チェックを行う。
    /// </summary>
    /// <param name="url">Markdown 内の URL。</param>
    /// <param name="context">変換コンテキスト。</param>
    /// <returns>リソースのフルパス。</returns>
    static string ResolveResourcePath(string url, RenderContext context)
    {
        string decoded = Uri.UnescapeDataString(url);
        string normalized = decoded.Replace('\\', '/');

        if (!normalized.StartsWith("../_resources/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"APPERROR: Attachment path must start with ../_resources/. Path: {url}");
        }

        string fullPath = Path.GetFullPath(Path.Combine(context.SourceDirectory, decoded));
        string resourcesRoot = context.ResourcesDirectoryFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(resourcesRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"APPERROR: Attachment path is outside ../_resources/. Path: {url}");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"APPERROR: Attachment file not found. Path: {fullPath}");
        }

        return fullPath;
    }

    /// <summary>
    /// URL が Web リンク (mailto/tel/file を含む) かどうかを判定する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>Web リンクなら true。</returns>
    static bool IsWebLink(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 自動リンクとして認識された URL を分割する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <param name="trimmedUrl">URL として扱う部分。</param>
    /// <param name="trailingText">URL 以降の文字列。</param>
    /// <returns>分割に成功した場合は true。</returns>
    static bool TrySplitAutoLinkUrl(string url, out string trimmedUrl, out string trailingText)
    {
        trimmedUrl = url;
        trailingText = string.Empty;

        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        int endIndex = FindAutoLinkEndIndex(url);
        if (endIndex <= 0)
        {
            return false;
        }

        int trimmedEnd = TrimAutoLinkTrailingPunctuation(url, endIndex);
        if (trimmedEnd <= 0)
        {
            return false;
        }

        if (trimmedEnd >= url.Length)
        {
            return false;
        }

        trimmedUrl = url[..trimmedEnd];
        trailingText = url[trimmedEnd..];
        return true;
    }

    /// <summary>
    /// 自動リンクの終端位置を検出する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>終端インデックス。</returns>
    static int FindAutoLinkEndIndex(string url)
    {
        for (int i = 0; i < url.Length; i++)
        {
            char ch = url[i];
            if (!IsUrlAllowedChar(ch))
            {
                return i;
            }

            if (ch == '%')
            {
                if (i + 2 >= url.Length || !IsHexDigit(url[i + 1]) || !IsHexDigit(url[i + 2]))
                {
                    return i;
                }

                i += 2;
            }
        }

        return url.Length;
    }

    /// <summary>
    /// URL 終端の句読点等を除外する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <param name="endIndex">終端候補。</param>
    /// <returns>トリム後の終端インデックス。</returns>
    static int TrimAutoLinkTrailingPunctuation(string url, int endIndex)
    {
        int trimmedEnd = endIndex;
        while (trimmedEnd > 0)
        {
            char ch = url[trimmedEnd - 1];
            if (!IsTrailingPunctuationCandidate(ch))
            {
                break;
            }

            if (ch == ')' && !HasUnmatchedClosing(url, trimmedEnd, '(', ')'))
            {
                break;
            }

            if (ch == ']' && !HasUnmatchedClosing(url, trimmedEnd, '[', ']'))
            {
                break;
            }

            if (ch == '}' && !HasUnmatchedClosing(url, trimmedEnd, '{', '}'))
            {
                break;
            }

            trimmedEnd--;
        }

        return trimmedEnd;
    }

    /// <summary>
    /// URL に含めることが許容される文字かどうかを判定する。
    /// </summary>
    /// <param name="ch">判定対象文字。</param>
    /// <returns>許容される場合は true。</returns>
    static bool IsUrlAllowedChar(char ch)
    {
        if (ch > 0x7f)
        {
            return false;
        }

        if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
        {
            return true;
        }

        return ch switch
        {
            '-' or '.' or '_' or '~' or ':' or '/' or '?' or '#' or '[' or ']' or '@' or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=' or '%' => true,
            _ => false,
        };
    }

    /// <summary>
    /// 16 進数文字かどうかを判定する。
    /// </summary>
    /// <param name="ch">判定対象文字。</param>
    /// <returns>16 進数なら true。</returns>
    static bool IsHexDigit(char ch)
    {
        return (ch >= '0' && ch <= '9')
            || (ch >= 'a' && ch <= 'f')
            || (ch >= 'A' && ch <= 'F');
    }

    /// <summary>
    /// URL 終端として除外すべき文字候補かどうかを判定する。
    /// </summary>
    /// <param name="ch">判定対象文字。</param>
    /// <returns>除外候補なら true。</returns>
    static bool IsTrailingPunctuationCandidate(char ch)
    {
        return ch switch
        {
            '.' or ',' or ';' or ':' or '!' or '?' or '"' or '\'' or '。' or '、' or '，' or '．' or '！' or '？' or '」' or '』' or '）' or '】' or '｝' or '〉' or '》' or '＞' or '…' => true,
            ')' or ']' or '}' => true,
            _ => false,
        };
    }

    /// <summary>
    /// 閉じ括弧が未対応かどうかを判定する。
    /// </summary>
    /// <param name="text">対象文字列。</param>
    /// <param name="endIndex">検索対象の終端。</param>
    /// <param name="openChar">開始括弧。</param>
    /// <param name="closeChar">終了括弧。</param>
    /// <returns>未対応の閉じ括弧がある場合は true。</returns>
    static bool HasUnmatchedClosing(string text, int endIndex, char openChar, char closeChar)
    {
        int openCount = 0;
        int closeCount = 0;
        for (int i = 0; i < endIndex; i++)
        {
            char ch = text[i];
            if (ch == openChar)
            {
                openCount++;
            }
            else if (ch == closeChar)
            {
                closeCount++;
            }
        }

        return closeCount > openCount;
    }

    /// <summary>
    /// リンク先が数字のみかどうかを判定する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>数字のみなら true。</returns>
    static bool IsNumericOnlyLinkDestination(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        foreach (char ch in url)
        {
            if (ch < '0' || ch > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// リンク先が "max" かどうかを判定する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>"max" なら true。</returns>
    static bool IsMaxLinkDestination(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return string.Equals(url.Trim(), "max", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// リンク先が "x-" で始まるかどうかを判定する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>"x-" で始まるなら true。</returns>
    static bool IsXSchemeLinkDestination(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.TrimStart().StartsWith("x-", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 画像 URL が http/https の絶対 URL かどうかを判定する。
    /// </summary>
    /// <param name="url">URL 文字列。</param>
    /// <returns>http/https の絶対 URL なら true。</returns>
    static bool IsHttpImageUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// インラインから表示用の平文文字列を取得する。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <returns>平文文字列。</returns>
    static string GetInlinePlainText(Inline? inline)
    {
        var builder = new StringBuilder();
        AppendInlinePlainText(inline, builder);
        return builder.ToString();
    }

    /// <summary>
    /// タイトル抽出用にインラインを平文化する。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <returns>平文文字列。</returns>
    static string GetInlinePlainTextForTitle(Inline? inline)
    {
        var builder = new StringBuilder();
        AppendInlinePlainTextForTitle(inline, builder);
        return builder.ToString();
    }

    /// <summary>
    /// インラインを再帰的に平文化して追加する。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <param name="builder">出力先。</param>
    static void AppendInlinePlainText(Inline? inline, StringBuilder builder)
    {
        for (Inline? current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case LineBreakInline:
                    builder.Append(' ');
                    break;
                case CodeInline codeInline:
                    builder.Append(codeInline.Content);
                    break;
                case HtmlEntityInline entityInline:
                    string entityText = entityInline.Transcoded.ToString();
                    if (string.IsNullOrEmpty(entityText))
                    {
                        entityText = entityInline.Original.ToString();
                    }

                    if (string.IsNullOrEmpty(entityText))
                    {
                        break;
                    }

                    if (string.Equals(entityText, "&nbsp;", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append('\u00A0');
                    }
                    else
                    {
                        builder.Append(entityText);
                    }
                    break;
                case HtmlInline htmlInline:
                    string tag = htmlInline.Tag ?? string.Empty;
                    if (IsLineBreakHtmlTag(tag))
                    {
                        builder.Append(' ');
                    }
                    else
                    {
                        builder.Append(tag);
                    }
                    break;
                case ContainerInline container:
                    AppendInlinePlainText(container.FirstChild, builder);
                    break;
                default:
                    builder.Append(current.ToString());
                    break;
            }
        }
    }

    /// <summary>
    /// タイトル抽出用にインラインを平文化して追加する。
    /// </summary>
    /// <param name="inline">インライン列の先頭。</param>
    /// <param name="builder">出力先。</param>
    static void AppendInlinePlainTextForTitle(Inline? inline, StringBuilder builder)
    {
        for (Inline? current = inline; current != null; current = current.NextSibling)
        {
            switch (current)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case LineBreakInline:
                    builder.Append(' ');
                    break;
                case CodeInline codeInline:
                    builder.Append(codeInline.Content);
                    break;
                case HtmlEntityInline entityInline:
                    string entityText = entityInline.Transcoded.ToString();
                    if (string.IsNullOrEmpty(entityText))
                    {
                        entityText = entityInline.Original.ToString();
                    }

                    if (string.IsNullOrEmpty(entityText))
                    {
                        break;
                    }

                    if (string.Equals(entityText, "&nbsp;", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append('\u00A0');
                    }
                    else
                    {
                        builder.Append(entityText);
                    }
                    break;
                case HtmlInline htmlInline:
                    string tag = htmlInline.Tag ?? string.Empty;
                    if (IsLineBreakHtmlTag(tag))
                    {
                        builder.Append(' ');
                    }
                    break;
                case LinkInline linkInline:
                    if (!linkInline.IsImage)
                    {
                        int beforeLength = builder.Length;
                        AppendInlinePlainTextForTitle(linkInline.FirstChild, builder);
                        if (builder.Length == beforeLength && !string.IsNullOrEmpty(linkInline.Url))
                        {
                            builder.Append(linkInline.Url);
                        }
                    }
                    break;
                case ContainerInline container:
                    AppendInlinePlainTextForTitle(container.FirstChild, builder);
                    break;
                default:
                    builder.Append(current.ToString());
                    break;
            }
        }
    }

    /// <summary>
    /// HTML の &lt;br&gt; タグかどうかを判定する。
    /// </summary>
    /// <param name="tag">タグ文字列。</param>
    /// <returns>&lt;br&gt; なら true。</returns>
    static bool IsLineBreakHtmlTag(string tag)
    {
        string trimmed = tag.Trim().ToLowerInvariant();
        return trimmed == "<br>" || trimmed == "<br/>" || trimmed == "<br />";
    }

    /// <summary>
    /// 下線開始タグかどうかを判定する。
    /// </summary>
    /// <param name="tag">タグ文字列。</param>
    /// <returns>開始タグなら true。</returns>
    static bool IsUnderlineStartTag(string tag)
    {
        string trimmed = tag.Trim().ToLowerInvariant();
        return trimmed == "<ins>" || trimmed == "<u>";
    }

    /// <summary>
    /// 下線終了タグかどうかを判定する。
    /// </summary>
    /// <param name="tag">タグ文字列。</param>
    /// <returns>終了タグなら true。</returns>
    static bool IsUnderlineEndTag(string tag)
    {
        string trimmed = tag.Trim().ToLowerInvariant();
        return trimmed == "</ins>" || trimmed == "</u>";
    }

    /// <summary>
    /// ランダムな 32 文字の 16 進文字列を生成する。
    /// </summary>
    /// <returns>16 進文字列。</returns>
    static string GenerateRandomHex32()
    {
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return System.Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// テキストを正規化して読み込む。
    /// </summary>
    /// <param name="path">読み込むファイルパス。</param>
    /// <returns>正規化済み文字列。</returns>
    static string ReadAllTextNormalized(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, true), true);
        string text = reader.ReadToEnd();
        return NormalizeNewlines(text);
    }

    /// <summary>
    /// 改行コードを LF に正規化する。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>正規化文字列。</returns>
    static string NormalizeNewlines(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    /// <summary>
    /// 警告ログを追記する。
    /// </summary>
    /// <param name="warningsLogFilePath">警告ログファイル。</param>
    /// <param name="srcFileName">元ファイル名。</param>
    /// <param name="warnings">警告一覧。</param>
    static void AppendWarningsLogIfNeeded(string warningsLogFilePath, string srcFileName, List<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        string? dir = Path.GetDirectoryName(warningsLogFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var builder = new StringBuilder();
        builder.Append("★ ").AppendLine(srcFileName);

        for (int i = 0; i < warnings.Count; i++)
        {
            string normalized = NormalizeNewlines(warnings[i]);
            foreach (string line in normalized.Split('\n'))
            {
                builder.Append("    ").AppendLine(line);
            }

            if (i < warnings.Count - 1)
            {
                builder.AppendLine("    ");
            }
        }

        builder.AppendLine();
        File.AppendAllText(warningsLogFilePath, builder.ToString(), new UTF8Encoding(true));
    }
}

/// <summary>
/// Front Matter の情報を保持するデータ構造。
/// </summary>
sealed class FrontMatterInfo
{
    /// <summary>
    /// タイトル。
    /// </summary>
    public string Title = string.Empty;

    /// <summary>
    /// 作成日時 (UTC)。
    /// </summary>
    public DateTimeOffset CreatedUtc;

    /// <summary>
    /// 更新日時 (UTC)。
    /// </summary>
    public DateTimeOffset UpdatedUtc;
}

/// <summary>
/// 変換時のコンテキスト情報。
/// </summary>
sealed class RenderContext
{
    /// <summary>
    /// 元 Markdown ファイルのフルパス。
    /// </summary>
    public string SourceMarkdownPath = string.Empty;

    /// <summary>
    /// 元 Markdown ファイルのディレクトリ。
    /// </summary>
    public string SourceDirectory = string.Empty;

    /// <summary>
    /// HTML 出力ディレクトリ。
    /// </summary>
    public string OutputDirectory = string.Empty;

    /// <summary>
    /// 添付ファイル出力ディレクトリ。
    /// </summary>
    public string AttachmentsDirectory = string.Empty;

    /// <summary>
    /// _resources のフルパス。
    /// </summary>
    public string ResourcesDirectoryFullPath = string.Empty;

    /// <summary>
    /// 添付ファイルの変換済みファイル名マップ。
    /// </summary>
    public Dictionary<string, string> AttachmentNameBySourcePath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 画像情報のキャッシュ。
    /// </summary>
    public Dictionary<string, ImageInfo> ImageInfoBySourcePath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 警告一覧。
    /// </summary>
    public List<string> Warnings = new();
}

/// <summary>
/// 添付ファイル情報。
/// </summary>
sealed class AttachmentInfo
{
    /// <summary>
    /// 元ファイルのフルパス。
    /// </summary>
    public string SourceFullPath = string.Empty;

    /// <summary>
    /// 出力ファイル名。
    /// </summary>
    public string OutputFileName = string.Empty;
}

/// <summary>
/// 画像情報。
/// </summary>
sealed class ImageInfo
{
    /// <summary>
    /// 元ファイルのフルパス。
    /// </summary>
    public string SourceFullPath = string.Empty;

    /// <summary>
    /// 出力ファイル名。
    /// </summary>
    public string OutputFileName = string.Empty;

    /// <summary>
    /// 画像幅。
    /// </summary>
    public int Width;

    /// <summary>
    /// 画像高さ。
    /// </summary>
    public int Height;

    /// <summary>
    /// 画像サイズを取得できたかどうか。
    /// </summary>
    public bool HasSize;
}

/// <summary>
/// HTML 出力用ライター。
/// </summary>
sealed class HtmlWriter
{
    readonly StringBuilder _builder = new();
    int _indentLevel;
    const int IndentSize = 4;

    /// <summary>
    /// インデントを 1 段増やす。
    /// </summary>
    public void IncreaseIndent()
    {
        _indentLevel++;
    }

    /// <summary>
    /// インデントを 1 段減らす。
    /// </summary>
    public void DecreaseIndent()
    {
        if (_indentLevel > 0)
        {
            _indentLevel--;
        }
    }

    /// <summary>
    /// 1 行書き出す。
    /// </summary>
    /// <param name="line">出力内容。</param>
    public void WriteLine(string line)
    {
        if (_indentLevel > 0)
        {
            _builder.Append(' ', _indentLevel * IndentSize);
        }

        _builder.Append(line).Append('\n');
    }

    /// <summary>
    /// 空行を書き出す。
    /// </summary>
    public void WriteBlankLine()
    {
        _builder.Append('\n');
    }

    /// <summary>
    /// 生成済み HTML を取得する。
    /// </summary>
    /// <returns>HTML 文字列。</returns>
    public override string ToString()
    {
        return _builder.ToString();
    }
}

/// <summary>
/// インライン出力の共通インターフェイス。
/// </summary>
interface IInlineWriter
{
    /// <summary>
    /// テキストを書き出す。
    /// </summary>
    /// <param name="text">テキスト。</param>
    /// <param name="treatWhitespaceAsVisible">空白を可視とみなすか。</param>
    void WriteText(string text, bool treatWhitespaceAsVisible);

    /// <summary>
    /// 生 HTML を書き出す。
    /// </summary>
    /// <param name="html">HTML 文字列。</param>
    /// <param name="isVisible">可視要素として扱うか。</param>
    void WriteRawHtml(string html, bool isVisible);

    /// <summary>
    /// 画像 HTML を書き出す。
    /// </summary>
    /// <param name="html">画像 HTML。</param>
    void WriteImageHtml(string html);

    /// <summary>
    /// 開始タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    /// <param name="attributes">属性。</param>
    void OpenTag(string tagName, string? attributes = null);

    /// <summary>
    /// 終了タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    void CloseTag(string tagName);

    /// <summary>
    /// 行区切りを出力する。
    /// </summary>
    void LineBreak();
}

/// <summary>
/// 段落分割用のインライン出力結果。
/// </summary>
sealed class InlineSegment
{
    /// <summary>
    /// 出力 HTML。
    /// </summary>
    public StringBuilder Builder = new();

    /// <summary>
    /// 画像のみを含むかの判定用フラグ。
    /// </summary>
    public bool HasImage;

    /// <summary>
    /// 画像以外の可視コンテンツを含むかの判定用フラグ。
    /// </summary>
    public bool HasNonImageContent;

    /// <summary>
    /// 可視コンテンツを含むかどうか。
    /// </summary>
    public bool HasVisibleContent => HasImage || HasNonImageContent;

    /// <summary>
    /// 出力 HTML 文字列。
    /// </summary>
    public string Content => Builder.ToString();
}

/// <summary>
/// インラインを複数行に分割するためのライター。
/// </summary>
sealed class SegmentedInlineWriter : IInlineWriter
{
    readonly List<InlineSegment> _segments = new();
    readonly List<TagInfo> _openTags = new();
    InlineSegment _current;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    public SegmentedInlineWriter()
    {
        _current = new InlineSegment();
        _segments.Add(_current);
    }

    /// <summary>
    /// 分割済みセグメント一覧。
    /// </summary>
    public IReadOnlyList<InlineSegment> Segments => _segments;

    /// <summary>
    /// テキストを書き出す。
    /// </summary>
    /// <param name="text">テキスト。</param>
    /// <param name="treatWhitespaceAsVisible">空白を可視として扱うか。</param>
    public void WriteText(string text, bool treatWhitespaceAsVisible)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _current.Builder.Append(HtmlUtility.EscapeTextPreserveSpaces(text));
        if (treatWhitespaceAsVisible || !HtmlUtility.IsWhitespaceOnly(text))
        {
            _current.HasNonImageContent = true;
        }
    }

    /// <summary>
    /// 生 HTML を書き出す。
    /// </summary>
    /// <param name="html">HTML 文字列。</param>
    /// <param name="isVisible">可視要素として扱うか。</param>
    public void WriteRawHtml(string html, bool isVisible)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        _current.Builder.Append(html);
        if (isVisible)
        {
            _current.HasNonImageContent = true;
        }
    }

    /// <summary>
    /// 画像 HTML を書き出す。
    /// </summary>
    /// <param name="html">画像 HTML。</param>
    public void WriteImageHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        _current.Builder.Append(html);
        _current.HasImage = true;
    }

    /// <summary>
    /// 開始タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    /// <param name="attributes">属性。</param>
    public void OpenTag(string tagName, string? attributes = null)
    {
        _current.Builder.Append('<').Append(tagName);
        if (!string.IsNullOrWhiteSpace(attributes))
        {
            _current.Builder.Append(' ').Append(attributes);
        }

        _current.Builder.Append('>');
        _openTags.Add(new TagInfo(tagName, attributes));
    }

    /// <summary>
    /// 終了タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    public void CloseTag(string tagName)
    {
        _current.Builder.Append("</").Append(tagName).Append('>');
        if (_openTags.Count > 0)
        {
            _openTags.RemoveAt(_openTags.Count - 1);
        }
    }

    /// <summary>
    /// 行区切りを出力する。
    /// </summary>
    public void LineBreak()
    {
        WriteTemporaryClosingTags();
        _current = new InlineSegment();
        _segments.Add(_current);
        ReopenTags();
    }

    /// <summary>
    /// 出力完了処理。
    /// </summary>
    public void Finish()
    {
    }

    void WriteTemporaryClosingTags()
    {
        for (int i = _openTags.Count - 1; i >= 0; i--)
        {
            _current.Builder.Append("</").Append(_openTags[i].TagName).Append('>');
        }
    }

    void ReopenTags()
    {
        foreach (TagInfo tag in _openTags)
        {
            _current.Builder.Append('<').Append(tag.TagName);
            if (!string.IsNullOrWhiteSpace(tag.Attributes))
            {
                _current.Builder.Append(' ').Append(tag.Attributes);
            }

            _current.Builder.Append('>');
        }
    }
}

/// <summary>
/// インラインを 1 行文字列として出力するライター。
/// </summary>
sealed class InlineStringWriter : IInlineWriter
{
    readonly StringBuilder _builder = new();

    /// <summary>
    /// テキストを書き出す。
    /// </summary>
    /// <param name="text">テキスト。</param>
    /// <param name="treatWhitespaceAsVisible">空白を可視として扱うか。</param>
    public void WriteText(string text, bool treatWhitespaceAsVisible)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _builder.Append(HtmlUtility.EscapeTextPreserveSpaces(text));
    }

    /// <summary>
    /// 生 HTML を書き出す。
    /// </summary>
    /// <param name="html">HTML 文字列。</param>
    /// <param name="isVisible">可視要素として扱うか。</param>
    public void WriteRawHtml(string html, bool isVisible)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        _builder.Append(html);
    }

    /// <summary>
    /// 画像 HTML を書き出す。
    /// </summary>
    /// <param name="html">画像 HTML。</param>
    public void WriteImageHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        _builder.Append(html);
    }

    /// <summary>
    /// 開始タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    /// <param name="attributes">属性。</param>
    public void OpenTag(string tagName, string? attributes = null)
    {
        _builder.Append('<').Append(tagName);
        if (!string.IsNullOrWhiteSpace(attributes))
        {
            _builder.Append(' ').Append(attributes);
        }

        _builder.Append('>');
    }

    /// <summary>
    /// 終了タグを書き出す。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    public void CloseTag(string tagName)
    {
        _builder.Append("</").Append(tagName).Append('>');
    }

    /// <summary>
    /// 行区切りを出力する。
    /// </summary>
    public void LineBreak()
    {
        _builder.Append(' ');
    }

    /// <summary>
    /// 生成済み文字列を取得する。
    /// </summary>
    /// <returns>HTML 文字列。</returns>
    public override string ToString()
    {
        return _builder.ToString();
    }
}

/// <summary>
/// タグの再オープン用情報。
/// </summary>
sealed class TagInfo
{
    /// <summary>
    /// タグ名。
    /// </summary>
    public string TagName;

    /// <summary>
    /// 属性文字列。
    /// </summary>
    public string? Attributes;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="tagName">タグ名。</param>
    /// <param name="attributes">属性。</param>
    public TagInfo(string tagName, string? attributes)
    {
        TagName = tagName;
        Attributes = attributes;
    }
}

/// <summary>
/// HTML エスケープ関連ユーティリティ。
/// </summary>
static class HtmlUtility
{
    /// <summary>
    /// 通常テキストを HTML 用にエスケープする。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>エスケープ済み文字列。</returns>
    public static string EscapeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// 連続空白を保持しつつ HTML エスケープする。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>エスケープ済み文字列。</returns>
    public static string EscapeTextPreserveSpaces(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace("\t", "    ").Replace("\r", string.Empty).Replace("\n", " ");
        var builder = new StringBuilder();
        int spaceRun = 0;

        foreach (char ch in normalized)
        {
            if (ch == ' ')
            {
                spaceRun++;
                continue;
            }

            AppendSpaceRun(builder, spaceRun);
            spaceRun = 0;

            switch (ch)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '\u00A0':
                    builder.Append("&nbsp;");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        AppendSpaceRun(builder, spaceRun);
        return builder.ToString();
    }

    /// <summary>
    /// コードブロック用の HTML エスケープを行う。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>エスケープ済み文字列。</returns>
    public static string EscapeCodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// HTML 属性用にエスケープする。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>エスケープ済み文字列。</returns>
    public static string EscapeAttribute(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    /// <summary>
    /// 空白のみかどうかを判定する。
    /// </summary>
    /// <param name="text">入力文字列。</param>
    /// <returns>空白のみなら true。</returns>
    public static bool IsWhitespaceOnly(string text)
    {
        foreach (char ch in text)
        {
            if (!char.IsWhiteSpace(ch))
            {
                return false;
            }
        }

        return true;
    }

    static void AppendSpaceRun(StringBuilder builder, int spaceRun)
    {
        if (spaceRun <= 0)
        {
            return;
        }

        if (spaceRun == 1)
        {
            builder.Append(' ');
            return;
        }

        for (int i = 0; i < spaceRun - 1; i++)
        {
            builder.Append("&nbsp;");
        }

        builder.Append(' ');
    }
}
