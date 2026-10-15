using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed record FileWriteOutcome(FileSnapshot Version, ToolFileChange? Change);
