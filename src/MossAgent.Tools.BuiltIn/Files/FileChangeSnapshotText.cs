using System.Text;

namespace MossAgent.Tools.BuiltIn.Files;

internal static class FileChangeSnapshotText
{
    public static string Decode(byte[] bytes)
    {
        try
        {
            Encoding encoding = new UTF8Encoding(false, true);
            var offset = 0;
            if (bytes.AsSpan().StartsWith(new byte[] { 255, 254, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); offset = 4; }
            else if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 254, 255 })) { encoding = new UTF32Encoding(true, true, true); offset = 4; }
            else if (bytes.AsSpan().StartsWith(new byte[] { 255, 254 })) { encoding = new UnicodeEncoding(false, true, true); offset = 2; }
            else if (bytes.AsSpan().StartsWith(new byte[] { 254, 255 })) { encoding = new UnicodeEncoding(true, true, true); offset = 2; }
            else if (bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) offset = 3;
            var text = encoding.GetString(bytes.AsSpan(offset));
            if (text.Contains('\0')) throw new DecoderFallbackException();
            return text;
        }
        catch (DecoderFallbackException) { throw new FileEditException("snapshot_review_unsupported", "ReviewChangeUnsupported"); }
    }
}
