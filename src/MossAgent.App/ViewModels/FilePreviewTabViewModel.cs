using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class FilePreviewTabViewModel : ObservableObject, IDisposable
{
    private bool _selected;
    private bool _loading;
    private bool _ready;
    private string _text = string.Empty;
    private string _status = string.Empty;
    private Bitmap? _image;
    private bool _disposed;
    private bool _fit = true;
    private ToolArtifact? _artifact;
    private double _scale = 1;
    private int _documentPages;
    private int _documentTotalPages;
    private bool _documentTruncated;

    public FilePreviewTabViewModel(string path, string name, string displayPath, string? relativePath = null)
    {
        Path = path;
        Name = name;
        DisplayPath = displayPath;
        RelativePath = relativePath;
        ZoomInCommand = new RelayCommand(() => SetZoom(Math.Min(4, (IsFit ? 1 : Scale) * 1.25)), () => CanZoom && (IsFit || Scale < 4));
        ZoomOutCommand = new RelayCommand(() => SetZoom(Math.Max(.1, (IsFit ? 1 : Scale) / 1.25)), () => CanZoom && (IsFit || Scale > .1));
        FitCommand = new RelayCommand(() => { if (CanZoom) { IsFit = true; NotifyZoom(); } }, () => CanZoom);
        ActualSizeCommand = new RelayCommand(() => SetZoom(1), () => CanZoom);
    }

    public FilePreviewSearchViewModel Search { get; } = new();
    public IRelayCommand ZoomInCommand { get; }
    public IRelayCommand ZoomOutCommand { get; }
    public IRelayCommand FitCommand { get; }
    public IRelayCommand ActualSizeCommand { get; }
    public bool IsFit { get => _fit; private set => SetProperty(ref _fit, value); }
    public double Scale { get => _scale; private set => SetProperty(ref _scale, value); }
    public string ZoomLabel => IsFit ? InspectorText.Get("PreviewFit") : $"{Scale:P0}";
    private bool CanZoom => !_disposed && IsReady && IsImage;

    public string Path { get; }
    public string Name { get; }
    public string DisplayPath { get; }
    public string ParentDisplayPath => DisplayPath.EndsWith(" > " + Name, StringComparison.Ordinal)
        ? DisplayPath[..^(Name.Length + 3)] : string.Empty;
    public string? RelativePath { get; }
    public bool IsSelected { get => _selected; internal set => SetProperty(ref _selected, value); }
    public bool IsLoading { get => _loading; internal set => SetProperty(ref _loading, value); }
    public bool IsReady
    {
        get => _ready;
        internal set
        {
            if (!SetProperty(ref _ready, value)) return;
            Search.SetAvailable(!_disposed && value && !IsImage && !IsDiff);
            NotifyZoom();
        }
    }
    public string Text { get => _text; internal set { if (SetProperty(ref _text, value)) Search.Reset(value); } }
    public string Status { get => _status; internal set => SetProperty(ref _status, value); }
    public Bitmap? Image
    {
        get => _image;
        internal set
        {
            if (ReferenceEquals(_image, value)) return;
            var previous = _image;
            SetProperty(ref _image, value);
            OnPropertyChanged(nameof(IsImage));
            IsFit = true;
            Scale = 1;
            Search.SetAvailable(!_disposed && IsReady && !IsImage && !IsDiff);
            NotifyZoom();
            previous?.Dispose();
        }
    }
    public bool IsImage => Image is not null;
    public bool IsDocument => Artifact?.MediaType == "application/pdf";
    public int DocumentProcessedPages { get => _documentPages; private set => SetProperty(ref _documentPages, value); }
    public int DocumentTotalPages { get => _documentTotalPages; private set => SetProperty(ref _documentTotalPages, value); }
    public bool IsDocumentTruncated { get => _documentTruncated; private set => SetProperty(ref _documentTruncated, value); }
    public string DocumentPageStatus => IsDocument
        ? InspectorText.Format("PreviewPdfPages", DocumentProcessedPages, DocumentTotalPages) : string.Empty;
    internal CancellationTokenSource? Loading { get; set; }
    public bool IsDiff => Change is not null || Artifact?.MediaType == "text/x-diff";
    internal ToolFileChange? Change { get; init; }
    internal ConversationAttachment? Attachment { get; init; }
    internal AttachmentThumbnailSource? AttachmentPreview { get; init; }
    internal ToolArtifact? Artifact
    {
        get => _artifact;
        set { _artifact = value; OnPropertyChanged(nameof(IsDiff)); OnPropertyChanged(nameof(IsDocument));
            OnPropertyChanged(nameof(DocumentPageStatus)); Search.SetAvailable(!_disposed && IsReady && !IsImage && !IsDiff); }
    }

    internal void SetDocumentStats(int processedPages, int totalPages, bool truncated)
    {
        DocumentProcessedPages = processedPages;
        DocumentTotalPages = totalPages;
        IsDocumentTruncated = truncated;
        OnPropertyChanged(nameof(DocumentPageStatus));
    }
    internal bool IsStoredArtifact { get; set; }

    private void SetZoom(double scale)
    {
        if (!CanZoom) return;
        Scale = scale;
        IsFit = false;
        NotifyZoom();
    }

    private void NotifyZoom()
    {
        OnPropertyChanged(nameof(ZoomLabel));
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitCommand.NotifyCanExecuteChanged();
        ActualSizeCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _disposed = true;
        Loading?.Cancel();
        Loading?.Dispose();
        Loading = null;
        Image = null;
        Text = string.Empty;
        Search.Reset(string.Empty);
        Search.SetAvailable(false);
        NotifyZoom();
    }
}
