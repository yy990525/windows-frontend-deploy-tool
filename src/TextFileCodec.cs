using System;
using System.IO;
using System.Text;

namespace FrontendDeployTool
{
    internal sealed class TextFileContent
    {
        public string Text { get; set; }
        public Encoding Encoding { get; set; }
        public string EncodingName { get; set; }
    }

    internal static class TextFileCodec
    {
        public const string Auto = "自动检测";
        public const string Utf8 = "UTF-8";
        public const string Utf8Bom = "UTF-8 BOM";
        public const string Gbk = "GBK";
        public const string Gb18030 = "GB18030";
        public const string Utf16Le = "UTF-16 LE";
        public const string Utf16Be = "UTF-16 BE";
        public const string SystemDefault = "系统默认";

        public static readonly string[] SupportedNames =
        {
            Auto,
            Utf8,
            Utf8Bom,
            Gbk,
            Gb18030,
            Utf16Le,
            Utf16Be,
            SystemDefault
        };

        public static TextFileContent Read(string path, string requestedEncoding)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (string.IsNullOrWhiteSpace(requestedEncoding) || requestedEncoding == Auto)
            {
                return DetectAndDecode(bytes);
            }

            Encoding encoding = GetEncoding(requestedEncoding);
            return new TextFileContent
            {
                Text = Decode(bytes, encoding),
                Encoding = encoding,
                EncodingName = requestedEncoding
            };
        }

        public static void Write(string path, string text, Encoding encoding)
        {
            File.WriteAllText(path, text ?? string.Empty, encoding);
        }

        public static Encoding GetEncoding(string name)
        {
            if (name == Utf8Bom)
            {
                return new UTF8Encoding(true, true);
            }
            if (name == Gbk)
            {
                return Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            }
            if (name == Gb18030)
            {
                return Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            }
            if (name == Utf16Le)
            {
                return new UnicodeEncoding(false, true, true);
            }
            if (name == Utf16Be)
            {
                return new UnicodeEncoding(true, true, true);
            }
            if (name == SystemDefault)
            {
                return Encoding.Default;
            }
            return new UTF8Encoding(false, true);
        }

        private static TextFileContent DetectAndDecode(byte[] bytes)
        {
            if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
            {
                Encoding encoding = new UTF8Encoding(true, true);
                return Create(bytes, encoding, Utf8Bom);
            }
            if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
            {
                Encoding encoding = new UTF32Encoding(false, true, true);
                return Create(bytes, encoding, "UTF-32 LE");
            }
            if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
            {
                Encoding encoding = new UTF32Encoding(true, true, true);
                return Create(bytes, encoding, "UTF-32 BE");
            }
            if (HasPrefix(bytes, 0xFF, 0xFE))
            {
                Encoding encoding = new UnicodeEncoding(false, true, true);
                return Create(bytes, encoding, Utf16Le);
            }
            if (HasPrefix(bytes, 0xFE, 0xFF))
            {
                Encoding encoding = new UnicodeEncoding(true, true, true);
                return Create(bytes, encoding, Utf16Be);
            }

            Encoding strictUtf8 = new UTF8Encoding(false, true);
            try
            {
                return Create(bytes, strictUtf8, Utf8);
            }
            catch (DecoderFallbackException)
            {
            }

            Encoding strictGbk = Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                return Create(bytes, strictGbk, Gbk);
            }
            catch (DecoderFallbackException)
            {
            }

            Encoding strictGb18030 = Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                return Create(bytes, strictGb18030, Gb18030);
            }
            catch (DecoderFallbackException)
            {
            }

            return Create(bytes, Encoding.Default, SystemDefault);
        }

        private static TextFileContent Create(byte[] bytes, Encoding encoding, string name)
        {
            return new TextFileContent
            {
                Text = Decode(bytes, encoding),
                Encoding = encoding,
                EncodingName = name
            };
        }

        private static string Decode(byte[] bytes, Encoding encoding)
        {
            string text = encoding.GetString(bytes);
            string preambleText = encoding.GetString(encoding.GetPreamble());
            if (!string.IsNullOrEmpty(preambleText) && text.StartsWith(preambleText, StringComparison.Ordinal))
            {
                text = text.Substring(preambleText.Length);
            }
            return text;
        }

        private static bool HasPrefix(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length)
            {
                return false;
            }
            for (int i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
