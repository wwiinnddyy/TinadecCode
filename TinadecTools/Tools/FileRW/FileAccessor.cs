using System.Buffers;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NLog;
using UtfUnknown;

namespace TinadecTools.Tools.FileRW;

/// <summary>
/// 一行的索引
/// </summary>
/// <param name="LineStart">本行开始的偏移</param>
/// <param name="LineEnd">本行结束的偏移（不包含换行符）</param>
/// <param name="NextStart">下一行开始的偏移</param>
internal record struct LineSpan(
    long LineStart,
    long LineEnd,
    long NextStart
);

/// <summary>
/// 一行的内容
/// </summary>
/// <param name="Content">具体内容（不包括换行符）</param>
/// <param name="LineNumber">行号</param>
/// <param name="LineLength">本行长度</param>
public record struct LineContent(
    string Content,
    int LineNumber,
    long LineLength
);

internal class FileAccessor : IDisposable
{
    private readonly FileStream file;
    private List<LineSpan> index = new();
    private readonly string filepath;
    private readonly SafeFileHandle handle;
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly UTF8Encoding utf8_no_bom = new(false);
    private const int stream_buffer_size = 128 * 1024;
    private const int text_buffer_size = 16 * 1024;

    static FileAccessor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// 打开一个文件。
    /// </summary>
    /// <param name="filepath">文件路径</param>
    internal FileAccessor(string filepath)
    {
        this.filepath = filepath;
        file = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete, 1024,
            FileOptions.Asynchronous);
        handle = file.SafeFileHandle;
        normalizeTextFile();
        buildIndex();
    }

    /// <summary>
    /// 打开文件时流式检测全文编码，并静默保存为 UTF-8（无 BOM）+ LF。
    /// </summary>
    private void normalizeTextFile()
    {
        if (file.Length == 0)
            return;

        file.Seek(0, SeekOrigin.Begin);
        var detection = CharsetDetector.DetectFromStream(file);
        var detected = detection.Detected;
        var encoding = detected?.Encoding ?? Encoding.UTF8;

        if (isUtf8Compatible(encoding) &&
            detected?.HasBOM != true &&
            !containsCarriageReturn())
        {
            file.Seek(0, SeekOrigin.Begin);
            return;
        }

        var tempPath = getTempPath();
        try
        {
            writeNormalizedTempFile(tempPath, encoding);

            file.SetLength(0);
            file.Seek(0, SeekOrigin.Begin);
            using var tempInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                stream_buffer_size, FileOptions.SequentialScan);
            tempInput.CopyTo(file, stream_buffer_size);
            file.Flush();
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException ex)
            {
                logger.Warn(ex, $"删除临时文件 {tempPath} 失败");
            }

            file.Seek(0, SeekOrigin.Begin);
        }
    }

    private static bool isUtf8Compatible(Encoding encoding)
    {
        return encoding.CodePage == Encoding.UTF8.CodePage ||
               encoding.CodePage == Encoding.ASCII.CodePage;
    }

    private string getTempPath()
    {
        var directory = Path.GetDirectoryName(filepath);
        var filename = Path.GetFileName(filepath);
        return Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $".{filename}.{Guid.NewGuid():N}.tmp");
    }

    private bool containsCarriageReturn()
    {
        file.Seek(0, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(stream_buffer_size);
        try
        {
            while (true)
            {
                var bytesRead = file.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    return false;

                for (var i = 0; i < bytesRead; i++)
                    if (buffer[i] == '\r')
                        return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            file.Seek(0, SeekOrigin.Begin);
        }
    }

    private void writeNormalizedTempFile(string tempPath, Encoding encoding)
    {
        file.Seek(0, SeekOrigin.Begin);

        using var reader = new StreamReader(file, encoding, false, stream_buffer_size, true);
        using var tempOutput = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            stream_buffer_size, FileOptions.SequentialScan);
        using var writer = new StreamWriter(tempOutput, utf8_no_bom, stream_buffer_size);
        var buffer = ArrayPool<char>.Shared.Rent(text_buffer_size);
        var firstChar = true;
        var previousWasCr = false;

        try
        {
            while (true)
            {
                var charsRead = reader.Read(buffer, 0, buffer.Length);
                if (charsRead == 0)
                    break;

                for (var i = 0; i < charsRead; i++)
                {
                    var ch = buffer[i];

                    if (firstChar)
                    {
                        firstChar = false;
                        if (ch == '\uFEFF')
                            continue;
                    }

                    if (previousWasCr)
                    {
                        previousWasCr = false;
                        if (ch == '\n')
                            continue;
                    }

                    if (ch == '\r')
                    {
                        writer.Write('\n');
                        previousWasCr = true;
                        continue;
                    }

                    writer.Write(ch);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 手动建立行号偏移索引
    /// </summary>
    private void buildIndex()
    {
        index.Clear();
        file.Seek(0, SeekOrigin.Begin);

        if (file.Length == 0)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(stream_buffer_size);
        try
        {
            long currentOffset = 0;
            long lineStart = 0;

            while (true)
            {
                var bytesRead = file.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                for (var i = 0; i < bytesRead; i++)
                {
                    currentOffset++;
                    if (buffer[i] == '\n')
                    {
                        index.Add(new LineSpan(lineStart, currentOffset - 1, currentOffset));
                        lineStart = currentOffset;
                    }
                }
            }

            if (lineStart < file.Length)
                index.Add(new LineSpan(lineStart, file.Length, file.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            file.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// 读取多个行范围（每个范围包括起始和结束行）
    /// </summary>
    /// <param name="lineRanges">行范围列表，Key=起始行，Value=结束行（都包括）</param>
    /// <returns></returns>
    public async Task<List<LineContent>> ReadLines(List<KeyValuePair<int, int>> lineRanges)
    {
        var contents = new List<LineContent>();

        foreach (var range in lineRanges)
        {
            var startLine = range.Key;
            var endLine = range.Value;

            // 验证范围
            if (startLine < 0 || startLine >= index.Count)
            {
                logger.Warn($"起始行号 {startLine} 超出范围 [0, {index.Count})");
                continue;
            }

            if (endLine < 0 || endLine >= index.Count)
            {
                logger.Warn($"结束行号 {endLine} 超出范围 [0, {index.Count})");
                continue;
            }

            if (startLine > endLine)
            {
                logger.Warn($"起始行号 {startLine} 大于结束行号 {endLine}");
                continue;
            }

            // 读取这个范围内的所有行
            for (var line = startLine; line <= endLine; line++)
            {
                var span = index[line];
                var length = span.LineEnd - span.LineStart;

                if (length <= 0)
                {
                    contents.Add(new LineContent(
                        string.Empty,
                        line,
                        0));
                    continue;
                }

                var buf = ArrayPool<byte>.Shared.Rent((int)length);
                try
                {
                    var memory = buf.AsMemory(0, (int)length);
                    var bytesRead =
                        await RandomAccess.ReadAsync(handle, memory, span.LineStart, CancellationToken.None);

                    if (bytesRead < length)
                        logger.Warn($"读取 {filepath} 偏移 {span.LineStart} 预期 {length} 字节，实际 {bytesRead}");

                    var content = Encoding.UTF8.GetString(buf, 0, bytesRead);
                    contents.Add(new LineContent(
                        content,
                        line,
                        bytesRead));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buf);
                }
            }
        }

        return contents;
    }

    /// <summary>
    /// 按字节偏移替换一段内容，替换后的长度可以与原长度不同。
    /// </summary>
    /// <param name="byteOffset">起始字节偏移</param>
    /// <param name="byteCount">要被替换掉的原始字节数</param>
    /// <param name="replacement">替换内容</param>
    public async Task<bool> ReplaceBytes(long byteOffset, long byteCount, ReadOnlyMemory<byte> replacement)
    {
        if (byteOffset < 0 || byteOffset > file.Length)
            throw new ArgumentOutOfRangeException(nameof(byteOffset), $"字节偏移 {byteOffset} 超出范围 [0, {file.Length}]");

        if (byteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "字节数量不能小于 0");

        if (byteCount > file.Length - byteOffset)
            throw new ArgumentOutOfRangeException(nameof(byteCount), $"字节数量 {byteCount} 超出范围，无法从偏移 {byteOffset} 开始替换");

        if (byteCount == 0 && replacement.Length == 0)
            return true;

        if (byteCount == replacement.Length)
        {
            await overwriteBytesInPlaceAsync(byteOffset, replacement);
        }
        else
        {
            await rewriteFileWithSegmentAsync(byteOffset, byteCount, replacement);
        }

        buildIndex();
        return true;
    }

    /// <summary>
    /// 删除指定字节段。
    /// </summary>
    public Task<bool> DeleteBytes(long byteOffset, long byteCount)
        => ReplaceBytes(byteOffset, byteCount, ReadOnlyMemory<byte>.Empty);

    /// <summary>
    /// 在指定字节偏移插入内容。
    /// </summary>
    public Task<bool> InsertBytes(long byteOffset, ReadOnlyMemory<byte> insertion)
        => ReplaceBytes(byteOffset, 0, insertion);

    /// <summary>
    /// 删除单行。
    /// </summary>
    public Task<bool> DeleteLine(int lineNumber)
        => DeleteLines(lineNumber, lineNumber);

    /// <summary>
    /// 删除连续多行。
    /// </summary>
    public Task<bool> DeleteLines(int startLine, int endLine)
    {
        var startSpan = getLineSpanOrThrow(startLine);
        var endSpan = getLineSpanOrThrow(endLine);

        if (startLine > endLine)
            throw new ArgumentException($"起始行号 {startLine} 不能大于结束行号 {endLine}");

        return ReplaceBytes(startSpan.LineStart, endSpan.NextStart - startSpan.LineStart, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// 在指定行后插入字节。
    /// </summary>
    public Task<bool> InsertBytesAfterLine(int lineNumber, ReadOnlyMemory<byte> insertion)
        => InsertBytes(getLineSpanOrThrow(lineNumber).NextStart, insertion);

    /// <summary>
    /// 在指定行前插入字节。
    /// </summary>
    public Task<bool> InsertBytesBeforeLine(int lineNumber, ReadOnlyMemory<byte> insertion)
        => InsertBytes(getLineSpanOrThrow(lineNumber).LineStart, insertion);

    /// <summary>
    /// 在指定行后插入若干行。
    /// </summary>
    public Task<bool> InsertLinesAfterLine(int lineNumber, IReadOnlyList<string> lines)
        => InsertBytesAfterLine(lineNumber, encodeLinesToUtf8Bytes(lines, true));

    /// <summary>
    /// 在指定行前插入若干行。
    /// </summary>
    public Task<bool> InsertLinesBeforeLine(int lineNumber, IReadOnlyList<string> lines)
        => InsertBytesBeforeLine(lineNumber, encodeLinesToUtf8Bytes(lines, true));

    /// <summary>
    /// 在两行之间插入字节。
    /// </summary>
    public Task<bool> InsertBytesBetweenLines(int upperLineNumber, int lowerLineNumber, ReadOnlyMemory<byte> insertion)
    {
        if (upperLineNumber < 0 || upperLineNumber >= index.Count)
            throw new ArgumentOutOfRangeException(nameof(upperLineNumber), $"行号 {upperLineNumber} 超出范围 [0, {index.Count})");

        if (lowerLineNumber < 0 || lowerLineNumber >= index.Count)
            throw new ArgumentOutOfRangeException(nameof(lowerLineNumber), $"行号 {lowerLineNumber} 超出范围 [0, {index.Count})");

        if (upperLineNumber >= lowerLineNumber)
            throw new ArgumentException($"上边行号 {upperLineNumber} 必须小于下边行号 {lowerLineNumber}");

        return InsertBytes(index[upperLineNumber].NextStart, insertion);
    }

    /// <summary>
    /// 在两行之间插入若干行。
    /// </summary>
    public Task<bool> InsertLinesBetweenLines(int upperLineNumber, int lowerLineNumber, IReadOnlyList<string> lines)
        => InsertBytesBetweenLines(upperLineNumber, lowerLineNumber, encodeLinesToUtf8Bytes(lines, true));

    /// <summary>
    /// 替换单个连续范围的行（写入后全量重建索引）
    /// </summary>
    /// <param name="startLine">起始行号（包括）</param>
    /// <param name="endLine">结束行号（包括）</param>
    /// <param name="content">新内容，按行传入且不包含换行符</param>
    public async Task<bool> ReplaceLines(int startLine, int endLine, IReadOnlyList<string> content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // 验证范围
        if (startLine < 0 || startLine >= index.Count)
            throw new ArgumentOutOfRangeException(nameof(startLine), $"起始行号 {startLine} 超出范围 [0, {index.Count})");

        if (endLine < 0 || endLine >= index.Count)
            throw new ArgumentOutOfRangeException(nameof(endLine), $"结束行号 {endLine} 超出范围 [0, {index.Count})");

        if (startLine > endLine)
            throw new ArgumentException($"起始行号 {startLine} 不能大于结束行号 {endLine}");

        var expectedCount = endLine - startLine + 1;
        if (content.Count != expectedCount)
            throw new ArgumentException($"内容行数 {content.Count} 与范围行数 {expectedCount} 不匹配");

        for (var i = 0; i < content.Count; i++)
        {
            var line = content[i];
            if (line.Contains('\n') || line.Contains('\r'))
                throw new ArgumentException($"内容行 {startLine + i} 包含换行符", nameof(content));
        }

        var rangeStart = index[startLine].LineStart;
        var rangeEndExclusive = index[endLine].NextStart;
        var currentLength = rangeEndExclusive - rangeStart;
        var hasTrailingNewline = index[endLine].NextStart > index[endLine].LineEnd;

        var replacementLength = content
            .Sum(line => Encoding.UTF8.GetByteCount(line))
            + Math.Max(0, content.Count - 1)
            + (hasTrailingNewline ? 1 : 0);

        if (replacementLength == currentLength)
        {
            await writeReplacementInPlaceAsync(rangeStart, content, hasTrailingNewline);
        }
        else
        {
            await rewriteFileWithReplacementAsync(rangeStart, rangeEndExclusive, content, hasTrailingNewline);
        }

        buildIndex();
        return true;
    }

    private async Task writeReplacementInPlaceAsync(long offset, IReadOnlyList<string> content, bool hasTrailingNewline)
    {
        file.Seek(offset, SeekOrigin.Begin);
        await using var writer = new StreamWriter(file, utf8_no_bom, stream_buffer_size, leaveOpen: true);
        await writeReplacementSectionAsync(writer, content, hasTrailingNewline);
        await writer.FlushAsync();
        file.Seek(0, SeekOrigin.End);
    }

    private async Task rewriteFileWithReplacementAsync(long rangeStart, long rangeEndExclusive, IReadOnlyList<string> content, bool hasTrailingNewline)
    {
        var originalLength = file.Length;
        var tempPath = getTempPath();

        try
        {
            await using (var tempOutput = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                stream_buffer_size, FileOptions.SequentialScan))
            {
                await copyRangeToStreamAsync(tempOutput, 0, rangeStart);
                await using (var writer = new StreamWriter(tempOutput, utf8_no_bom, stream_buffer_size, leaveOpen: true))
                {
                    await writeReplacementSectionAsync(writer, content, hasTrailingNewline);
                    await writer.FlushAsync();
                }
                await copyRangeToStreamAsync(tempOutput, rangeEndExclusive, originalLength - rangeEndExclusive);
                await tempOutput.FlushAsync();
            }

            file.SetLength(0);
            file.Seek(0, SeekOrigin.Begin);
            await using var tempInput = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                stream_buffer_size, FileOptions.SequentialScan);
            await tempInput.CopyToAsync(file, stream_buffer_size, CancellationToken.None);
            await file.FlushAsync(CancellationToken.None);
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException ex)
            {
                logger.Warn(ex, $"删除临时文件 {tempPath} 失败");
            }

            file.Seek(0, SeekOrigin.Begin);
        }
    }

    private async Task writeReplacementSectionAsync(StreamWriter writer, IReadOnlyList<string> content, bool hasTrailingNewline)
    {
        for (var i = 0; i < content.Count; i++)
        {
            await writer.WriteAsync(content[i]);

            if (i < content.Count - 1 || hasTrailingNewline)
                await writer.WriteAsync("\n");
        }
    }

    private async Task copyRangeToStreamAsync(Stream destination, long sourceOffset, long length)
    {
        if (length <= 0)
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(stream_buffer_size);
        try
        {
            var currentOffset = sourceOffset;
            var remaining = length;

            while (remaining > 0)
            {
                var chunk = (int)Math.Min(buffer.Length, remaining);
                var bytesRead = await RandomAccess.ReadAsync(handle, buffer.AsMemory(0, chunk), currentOffset, CancellationToken.None);
                if (bytesRead == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), CancellationToken.None);
                currentOffset += bytesRead;
                remaining -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void Dispose()
    {
        file.Dispose();
    }
}
