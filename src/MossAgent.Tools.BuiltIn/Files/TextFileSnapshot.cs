using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed record TextFileSnapshot(string Content, FileSnapshot Version, Encoding Encoding);
