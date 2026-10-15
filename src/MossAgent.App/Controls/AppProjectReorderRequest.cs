namespace MossAgent.App.Controls;

public sealed record AppProjectReorderRequest(object Scope, object Source, object Target, bool InsertAfter);
