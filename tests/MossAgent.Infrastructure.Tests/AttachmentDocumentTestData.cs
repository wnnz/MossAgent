using System.Text;

namespace MossAgent.Infrastructure.Tests;

internal static class AttachmentDocumentTestData
{
    public static byte[] Pdf(params string[] pageTexts) => Build(pageTexts, encrypted: false, paddingBytes: 0);

    public static byte[] EncryptedPdf(params string[] pageTexts) => Build(pageTexts, encrypted: true, paddingBytes: 0);

    public static byte[] PaddedPdf(int minimumByteCount) =>
        Build(["padded"], encrypted: false, paddingBytes: Math.Max(0, minimumByteCount - 500));

    private static byte[] Build(IReadOnlyList<string> pageTexts, bool encrypted, int paddingBytes)
    {
        var objects = new List<string>();
        var pageIds = pageTexts.Select((_, index) => 4 + index * 2).ToArray();
        var kids = string.Join(" ", pageIds.Select(id => $"{id} 0 R"));
        objects.Add("<</Type/Catalog/Pages 2 0 R>>");
        objects.Add($"<</Type/Pages/Kids[{kids}]/Count {pageIds.Length}>>");
        objects.Add("<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");
        foreach (var text in pageTexts)
        {
            var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            var stream = $"BT /F1 12 Tf 72 720 Td ({escaped}) Tj ET";
            var contentId = objects.Count + 2;
            objects.Add($"<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Resources<</Font<</F1 3 0 R>>>>/Contents {contentId} 0 R>>");
            objects.Add($"<</Length {stream.Length}>>\nstream\n{stream}\nendstream");
        }
        var encryptId = objects.Count + 1;
        if (paddingBytes > 0)
        {
            objects.Add($"<</Length {paddingBytes}>>\nstream\n{new string('x', paddingBytes)}\nendstream");
            encryptId = objects.Count + 1;
        }
        if (encrypted)
            objects.Add("<</Filter/Standard/V 1/R 2/O(AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA)/U(BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB)/P -4>>");
        var output = new MemoryStream();
        var offsets = new List<long>();
        void Write(string text) { var bytes = Encoding.ASCII.GetBytes(text); output.Write(bytes, 0, bytes.Length); }
        Write("%PDF-1.4\n");
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            Write($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write($"{offset:D10} 00000 n \n");
        Write(encrypted
            ? $"trailer\n<</Size {objects.Count + 1}/Root 1 0 R/Encrypt {encryptId} 0 R>>\nstartxref\n{xref}\n%%EOF\n"
            : $"trailer\n<</Size {objects.Count + 1}/Root 1 0 R>>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
