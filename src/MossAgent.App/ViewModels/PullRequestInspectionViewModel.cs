using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestInspectionViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IToolExecutor _tools;
    private ToolExecutionContext? _context;
    private PullRequestRevision? _revision;
    private PullRequestInspectionSection _selectedSection;
    private string _status = string.Empty;

    public PullRequestInspectionViewModel(IToolExecutor tools)
    {
        _tools = tools;
        Sections = new[] { "files", "conversation", "inline", "reviews", "activity", "runs", "statuses" }
            .Select(key => new PullRequestInspectionSection(key, PullRequestText.Get("Section_" + key))).ToArray();
        _selectedSection = Sections[0];
        Files = new((page, token) => ReadAsync<PullRequestFile>("source.pr_files", null, page, token), Fail);
        Conversation = new((page, token) => ReadAsync<PullRequestComment>("source.pr_comments", "conversation", page, token), Fail);
        Inline = new((page, token) => ReadAsync<PullRequestComment>("source.pr_comments", "inline", page, token), Fail);
        Reviews = new((page, token) => ReadAsync<PullRequestReview>("source.pr_reviews", null, page, token), Fail);
        Activity = new((page, token) => ReadAsync<PullRequestActivity>("source.pr_activity", null, page, token), Fail);
        Runs = new((page, token) => ReadAsync<PullRequestCheck>("source.pr_checks", "runs", page, token), Fail);
        Statuses = new((page, token) => ReadAsync<PullRequestCheck>("source.pr_checks", "statuses", page, token), Fail);
        Annotations = new(Files, Inline, () => SelectedSection = Sections[0]);
        Files.PropertyChanged += FileChanged;
    }

    public IReadOnlyList<PullRequestInspectionSection> Sections { get; }
    public PullRequestInspectionPageViewModel<PullRequestFile> Files { get; }
    public PullRequestInspectionPageViewModel<PullRequestComment> Conversation { get; }
    public PullRequestInspectionPageViewModel<PullRequestComment> Inline { get; }
    public PullRequestInspectionPageViewModel<PullRequestReview> Reviews { get; }
    public PullRequestInspectionPageViewModel<PullRequestActivity> Activity { get; }
    public PullRequestInspectionPageViewModel<PullRequestCheck> Runs { get; }
    public PullRequestInspectionPageViewModel<PullRequestCheck> Statuses { get; }
    public PullRequestAnnotationsViewModel Annotations { get; }
    public string Status { get => _status; private set { SetProperty(ref _status, value); OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => Status.Length > 0;
    public bool IsActive => _revision is not null;
    public PullRequestInspectionSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (value is null || !Sections.Contains(value) || !SetProperty(ref _selectedSection, value)) return;
            OnPropertyChanged(nameof(IsFiles)); OnPropertyChanged(nameof(IsComments)); OnPropertyChanged(nameof(IsChecks));
            OnPropertyChanged(nameof(IsInline)); OnPropertyChanged(nameof(IsDiscussion));
            OnPropertyChanged(nameof(IsReviews)); OnPropertyChanged(nameof(IsActivity));
            OnPropertyChanged(nameof(CurrentPage)); OnPropertyChanged(nameof(CurrentComments)); OnPropertyChanged(nameof(CurrentChecks));
        }
    }
    public bool IsFiles => SelectedSection.Key == "files";
    public bool IsComments => SelectedSection.Key is "conversation" or "inline";
    public bool IsInline => SelectedSection.Key == "inline";
    public bool IsDiscussion => SelectedSection.Key == "conversation";
    public bool IsChecks => SelectedSection.Key is "runs" or "statuses";
    public bool IsReviews => SelectedSection.Key == "reviews";
    public bool IsActivity => SelectedSection.Key == "activity";
    public object CurrentPage => SelectedSection.Key switch
    {
        "files" => Files,
        "conversation" => Conversation,
        "inline" => Inline,
        "reviews" => Reviews,
        "activity" => Activity,
        "statuses" => Statuses,
        _ => Runs
    };
    public PullRequestInspectionPageViewModel<PullRequestComment> CurrentComments => SelectedSection.Key == "inline" ? Inline : Conversation;
    public PullRequestInspectionPageViewModel<PullRequestCheck> CurrentChecks => SelectedSection.Key == "statuses" ? Statuses : Runs;
    public string PatchHint => PullRequestText.Get(Files.SelectedItem?.PatchState switch
    {
        PullRequestPatchState.Available => "PatchAvailable",
        PullRequestPatchState.Missing => "PatchMissing",
        PullRequestPatchState.TooLarge => "PatchTooLarge",
        PullRequestPatchState.Incomplete => "PatchIncomplete",
        _ => "SelectFile"
    });

    public void Attach(ToolExecutionContext context, PullRequestRevision revision)
    {
        Clear(); _context = context; _revision = revision;
        Annotations.Attach(revision);
        Files.Activate(); Conversation.Activate(); Inline.Activate(); Reviews.Activate(); Activity.Activate(); Runs.Activate(); Statuses.Activate();
        OnPropertyChanged(nameof(IsActive));
    }

    public void Clear()
    {
        _context = null; _revision = null; Status = string.Empty;
        Annotations.Clear();
        Files.Clear(); Conversation.Clear(); Inline.Clear(); Reviews.Clear(); Activity.Clear(); Runs.Clear(); Statuses.Clear();
        OnPropertyChanged(nameof(IsActive));
    }

    private void Fail(string code) { Clear(); Status = PullRequestText.Failure(code); }

    private async Task<PullRequestInspectionPage<T>> ReadAsync<T>(string tool, string? kind, int page, CancellationToken token)
    {
        var revision = _revision ?? throw new SourceReviewException("source_scope_denied");
        var context = _context ?? throw new SourceReviewException("source_scope_denied");
        var arguments = new Dictionary<string, object>
        { ["number"] = revision.Number, ["base_revision"] = revision.BaseRevision, ["head_revision"] = revision.HeadRevision, ["page"] = page };
        if (kind is not null) arguments["kind"] = kind;
        var request = new ToolRequest(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments));
        var result = await Task.Run(() => _tools.ExecuteAsync(request, context, token), token).ConfigureAwait(false);
        if (!result.IsSuccess) throw new SourceReviewException(result.ErrorCode ?? "source_unavailable");
        var payload = JsonSerializer.Deserialize<PullRequestInspectionPage<T>>(result.Content!) ?? throw new SourceReviewException("source_invalid_response");
        if (payload.Revision != revision || payload.Page != page) throw new SourceReviewException("source_revision_changed");
        return payload;
    }

    private void FileChanged(object? sender, PropertyChangedEventArgs args)
    { if (args.PropertyName == nameof(Files.SelectedItem)) OnPropertyChanged(nameof(PatchHint)); }

    public async Task DrainAsync() => await Task.WhenAll(Files.DrainAsync(), Conversation.DrainAsync(), Inline.DrainAsync(),
        Reviews.DrainAsync(), Activity.DrainAsync(), Runs.DrainAsync(), Statuses.DrainAsync()).ConfigureAwait(false);
    public void Dispose()
    {
        Files.PropertyChanged -= FileChanged;
        Annotations.Dispose();
        Clear(); Files.Dispose(); Conversation.Dispose(); Inline.Dispose(); Reviews.Dispose(); Activity.Dispose(); Runs.Dispose(); Statuses.Dispose();
    }
    public async ValueTask DisposeAsync() { Dispose(); await DrainAsync().ConfigureAwait(false); }
}
