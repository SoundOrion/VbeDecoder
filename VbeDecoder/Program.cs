using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

internal static class VbeDecoder
{
    // MrBrownstone "scrdec18.c" 相当のテーブルをC#に移植
    private static readonly byte[] RawData = new byte[]
    {
        0x64,0x37,0x69, 0x50,0x7E,0x2C, 0x22,0x5A,0x65, 0x4A,0x45,0x72,
        0x61,0x3A,0x5B, 0x5E,0x79,0x66, 0x5D,0x59,0x75, 0x5B,0x27,0x4C,
        0x42,0x76,0x45, 0x60,0x63,0x76, 0x23,0x62,0x2A, 0x65,0x4D,0x43,
        0x5F,0x51,0x33, 0x7E,0x53,0x42, 0x4F,0x52,0x20, 0x52,0x20,0x63,
        0x7A,0x26,0x4A, 0x21,0x54,0x5A, 0x46,0x71,0x38, 0x20,0x2B,0x79,
        0x26,0x66,0x32, 0x63,0x2A,0x57, 0x2A,0x58,0x6C, 0x76,0x7F,0x2B,
        0x47,0x7B,0x46, 0x25,0x30,0x52, 0x2C,0x31,0x4F, 0x29,0x6C,0x3D,
        0x69,0x49,0x70, 0x3F,0x3F,0x3F, 0x27,0x78,0x7B, 0x3F,0x3F,0x3F,
        0x67,0x5F,0x51, 0x3F,0x3F,0x3F, 0x62,0x29,0x7A, 0x41,0x24,0x7E,
        0x5A,0x2F,0x3B, 0x66,0x39,0x47, 0x32,0x33,0x41, 0x73,0x6F,0x77,
        0x4D,0x21,0x56, 0x43,0x75,0x5F, 0x71,0x28,0x26, 0x39,0x42,0x78,
        0x7C,0x46,0x6E, 0x53,0x4A,0x64, 0x48,0x5C,0x74, 0x31,0x48,0x67,
        0x72,0x36,0x7D, 0x6E,0x4B,0x68, 0x70,0x7D,0x35, 0x49,0x5D,0x22,
        0x3F,0x6A,0x55, 0x4B,0x50,0x3A, 0x6A,0x69,0x60, 0x2E,0x23,0x6A,
        0x7F,0x09,0x71, 0x28,0x70,0x6F, 0x35,0x65,0x49, 0x7D,0x74,0x5C,
        0x24,0x2C,0x5D, 0x2D,0x77,0x27, 0x54,0x44,0x59, 0x37,0x3F,0x25,
        0x7B,0x6D,0x7C, 0x3D,0x7C,0x23, 0x6C,0x43,0x6D, 0x34,0x38,0x28,
        0x6D,0x5E,0x31, 0x4E,0x5B,0x39, 0x2B,0x6E,0x7F, 0x30,0x57,0x36,
        0x6F,0x4C,0x54, 0x74,0x34,0x34, 0x6B,0x72,0x62, 0x4C,0x25,0x4E,
        0x33,0x56,0x30, 0x56,0x73,0x5E, 0x3A,0x68,0x73, 0x78,0x55,0x09,
        0x57,0x47,0x4B, 0x77,0x32,0x61, 0x3B,0x35,0x24, 0x44,0x2E,0x4D,
        0x2F,0x64,0x6B, 0x59,0x4F,0x44, 0x45,0x3B,0x21, 0x5C,0x2D,0x37,
        0x68,0x41,0x53, 0x36,0x61,0x58, 0x58,0x7A,0x48, 0x79,0x22,0x2E,
        0x09,0x60,0x50, 0x75,0x6B,0x2D, 0x38,0x4E,0x29, 0x55,0x3D,0x3F,
        0x51,0x67,0x2F
    };

    private static readonly byte[] PickEncoding = new byte[]
    {
        1,2,0,1,2,0,2,0,0,2,0,2,1,0,2,0,
        1,0,2,0,1,1,2,0,0,2,1,0,2,0,0,2,
        1,1,0,2,0,2,0,1,0,1,1,2,0,1,0,2,
        1,0,2,0,1,1,2,0,0,1,1,2,0,1,0,2
    };

    private static readonly byte[,] Transformed = new byte[3, 127];
    private const string Header = "#@~^";
    private const string Trailer = "^#~@";

    static VbeDecoder()
    {
        for (int i = 0; i < 32; i++)
            for (int j = 0; j < 3; j++)
                Transformed[j, i] = (byte)i;

        for (int i = 31; i <= 127; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int idx = (i - 31) * 3 + j;
                byte enc = RawData[idx];
                Transformed[j, enc] = (byte)(i == 31 ? 9 : i);
            }
        }
    }

    public static byte[] DecodeToBytes(string encodedWholeText)
    {
        if (encodedWholeText is null) throw new ArgumentNullException(nameof(encodedWholeText));

        var input = encodedWholeText.AsSpan();
        var output = new List<byte>(encodedWholeText.Length);

        int pos = 0;
        while (pos < input.Length)
        {
            int hdr = IndexOf(input, Header.AsSpan(), pos);
            if (hdr < 0)
            {
                AppendAsciiBytes(output, input.Slice(pos));
                break;
            }

            if (hdr > pos) AppendAsciiBytes(output, input.Slice(pos, hdr - pos));

            int blockStart = hdr + Header.Length;
            int trl = IndexOf(input, Trailer.AsSpan(), blockStart);
            if (trl < 0) throw new InvalidDataException("VBE trailer not found.");

            var block = input.Slice(blockStart, trl - blockStart);
            DecodeBlock(block, output);

            pos = trl + Trailer.Length;
        }

        return output.ToArray();
    }

    public static string DecodeToString(string encodedWholeText, Encoding? preferredEncoding = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var bytes = DecodeToBytes(encodedWholeText);

        // BOM優先
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        preferredEncoding ??= Encoding.GetEncoding(932); // cp932 既定
        return preferredEncoding.GetString(bytes);
    }

    public static void DecodeFile(string inputPath, string? outputPath = null, Encoding? preferredEncoding = null, bool rawBytes = false)
    {
        string text = File.ReadAllText(inputPath, Encoding.ASCII); // VBE構造はASCII
        if (rawBytes)
        {
            var bytes = DecodeToBytes(text);
            if (outputPath is null) Console.OpenStandardOutput().Write(bytes);
            else File.WriteAllBytes(outputPath, bytes);
        }
        else
        {
            var s = DecodeToString(text, preferredEncoding);
            if (outputPath is null) Console.Write(s);
            else
            {
                var enc = preferredEncoding ?? Encoding.UTF8;
                File.WriteAllText(outputPath, s, enc);
            }
        }
    }

    // ===== 内部処理 =====

    private static void DecodeBlock(ReadOnlySpan<char> block, List<byte> output)
    {
        int pos = 0;
        while (pos < block.Length)
        {
            if (!TryReadB64Len(block, ref pos, out int segLen))
            {
                if (pos + 1 < block.Length && block[pos] == '@' && IsEscapeChar(block[pos + 1]))
                {
                    output.Add(UnescapeToByte(block[pos + 1]));
                    pos += 2;
                    continue;
                }
                pos++;
                continue;
            }

            for (int i = 0; i < segLen; i++)
            {
                if (pos >= block.Length)
                    throw new InvalidDataException("VBE segment overruns block.");

                char ch = block[pos++];

                if (ch == '@' && pos < block.Length && IsEscapeChar(block[pos]))
                {
                    output.Add(UnescapeToByte(block[pos]));
                    pos++;
                    i--; // エスケープはカウント外
                    continue;
                }

                byte decoded = DecodeChar(ch, i);
                output.Add(decoded);
            }
        }
    }

    private static byte DecodeChar(char ch, int indexInSegment)
    {
        if (ch > 127) return (byte)(ch & 0xFF);
        byte set = PickEncoding[indexInSegment & 0x3F]; // 64周期
        byte c = (byte)ch;
        if (c >= 127) return c;
        return Transformed[set, c];
    }

    private static bool TryReadB64Len(ReadOnlySpan<char> s, ref int pos, out int length)
    {
        length = 0;
        if (pos + 8 > s.Length) return false;

        var span = s.Slice(pos, 8);
        if (!(IsB64Char(span[0]) && IsB64Char(span[1]) && IsB64Char(span[2]) && IsB64Char(span[3]) &&
              IsB64Char(span[4]) && IsB64Char(span[5]) && span[6] == '=' && span[7] == '='))
            return false;

        try
        {
            byte[] b = Convert.FromBase64String(new string(span));
            if (b.Length != 4) return false;
            length = b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24); // little-endian
            pos += 8;
            return true;
        }
        catch { return false; }
    }

    private static bool IsB64Char(char c) =>
        (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/';

    private static bool IsEscapeChar(char c) => c is '#' or '&' or '!' or '*' or '$';

    private static byte UnescapeToByte(char c) => c switch
    {
        '#' => (byte)'\r',
        '&' => (byte)'\n',
        '!' => (byte)'<',
        '*' => (byte)'>',
        '$' => (byte)'@',
        _ => (byte)'?'
    };

    private static void AppendAsciiBytes(List<byte> output, ReadOnlySpan<char> s)
    {
        for (int i = 0; i < s.Length; i++)
            output.Add((byte)(s[i] <= 0xFF ? s[i] : '?'));
    }

    private static int IndexOf(ReadOnlySpan<char> hay, ReadOnlySpan<char> needle, int start)
    {
        if (start < 0) start = 0;
        if (needle.Length == 0) return start;
        for (int i = start; i + needle.Length <= hay.Length; i++)
        {
            if (hay.Slice(i, needle.Length).SequenceEqual(needle))
                return i;
        }
        return -1;
    }
}

internal class Program
{
    private static void PrintUsage()
    {
        Console.Error.WriteLine(
@"VBE Decoder (.NET 8)
Usage:
  VbeDecode -i <input.vbe> [-o <output>] [--encoding <name>] [--raw]

Options:
  -i, --input      入力 .vbe ファイル（必須）
  -o, --output     出力ファイル（省略時は標準出力）
  --encoding       出力文字コード。例: utf-8, utf-16, cp932
                   省略時は BOM自動判定 → cp932
  --raw            復号後の生バイトをそのまま出力（文字コード変換しない）");
    }

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string? input = null;
        string? output = null;
        string? encName = null;
        bool raw = false;

        // シンプル引数パーサ
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-i":
                case "--input":
                    if (i + 1 >= args.Length) { PrintUsage(); return 2; }
                    input = args[++i];
                    break;
                case "-o":
                case "--output":
                    if (i + 1 >= args.Length) { PrintUsage(); return 2; }
                    output = args[++i];
                    break;
                case "--encoding":
                    if (i + 1 >= args.Length) { PrintUsage(); return 2; }
                    encName = args[++i];
                    break;
                case "--raw":
                    raw = true;
                    break;
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    // 未知オプションはエラー
                    Console.Error.WriteLine($"未知のオプション: {a}");
                    PrintUsage();
                    return 2;
            }
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            PrintUsage();
            return 2;
        }

        Encoding? enc = null;
        if (!string.IsNullOrWhiteSpace(encName))
        {
            try { enc = Encoding.GetEncoding(encName!); }
            catch
            {
                Console.Error.WriteLine($"未知のエンコーディング: {encName}");
                return 3;
            }
        }

        try
        {
            VbeDecoder.DecodeFile(input!, output, enc, raw);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("デコードに失敗しました: " + ex.Message);
            return 1;
        }
    }
}
