using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AttachmentPreviewViewModel : ObservableObject, IDisposable
{
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly AsyncRelayCommand<AttachmentPreviewSource?> _openCommand;
    private CancellationTokenSource? _loading;
    private AttachmentPreviewSource? _source;
    private Bitmap? _image;
    private bool _isOpen;
    private bool _isLoading;
    private bool _isFit = true;
    private double _scale = 1;
    private string _displayPath = string.Empty;
    private string _sizeLabel = string.Empty;
    private string _dimensions = string.Empty;
    private string _status = string.Empty;
    private int _generation;
    private bool _disposed;

    public AttachmentPreviewViewModel(TimeSpan? timeout = null, TimeProvider? timeProvider = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_timeout, TimeSpan.Zero);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _openCommand = new AsyncRelayCommand<AttachmentPreviewSource?>(
            (source, token) => OpenAsync(source, token), source => !_disposed && source is not null,
            AsyncRelayCommandOptions.AllowConcurrentExecutions);
        CloseCommand = new RelayCommand(Close, () => IsOpen);
        ZoomInCommand = new RelayCommand(() => SetZoom(Math.Min(4, (IsFit ? 1 : Scale) * 1.25)), () => CanZoom);
        ZoomOutCommand = new RelayCommand(() => SetZoom(Math.Max(.1, (IsFit ? 1 : Scale) / 1.25)), () => CanZoom);
        FitCommand = new RelayCommand(() => { if (CanZoom) { IsFit = true; NotifyZoom(); } }, () => CanZoom);
        ActualSizeCommand = new RelayCommand(() => SetZoom(1), () => CanZoom);
    }

    public IAsyncRelayCommand<AttachmentPreviewSource?> OpenCommand => _openCommand;
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand ZoomInCommand { get; }
    public IRelayCommand ZoomOutCommand { get; }
    public IRelayCommand FitCommand { get; }
    public IRelayCommand ActualSizeCommand { get; }
    public AttachmentPreviewSource? Source => _source;
    public Bitmap? Image { get => _image; private set => SetImage(value); }
    public bool IsOpen { get => _isOpen; private set { if (SetProperty(ref _isOpen, value)) { CloseCommand.NotifyCanExecuteChanged(); NotifyZoom(); } } }
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) { OnPropertyChanged(nameof(IsStateVisible)); NotifyZoom(); } } }
    public bool IsFit { get => _isFit; private set => SetProperty(ref _isFit, value); }
    public double Scale { get => _scale; private set => SetProperty(ref _scale, value); }
    public string DisplayPath
    {
        get => _displayPath;
        private set { if (SetProperty(ref _displayPath, value)) OnPropertyChanged(nameof(Title)); }
    }
    public string SizeLabel { get => _sizeLabel; private set => SetProperty(ref _sizeLabel, value); }
    public string Dimensions { get => _dimensions; private set => SetProperty(ref _dimensions, value); }
    public string Status { get => _status; private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(IsStateVisible)); } }
    public bool HasImage => _image is not null;
    public bool IsStateVisible => IsLoading || !HasImage;
    public string Title => string.Format(CultureInfo.CurrentCulture,
        WorkspaceText.Get("AttachmentPreviewTitleFormat"), Path.GetFileName(DisplayPath));
    public string ZoomLabel => IsFit ? WorkspaceText.Get("AttachmentPreviewFit")
        : Scale.ToString("P0", CultureInfo.CurrentCulture);
    private bool CanZoom => !_disposed && IsOpen && HasImage && !IsLoading;

    public async Task OpenAsync(AttachmentPreviewSource? source, CancellationToken commandToken = default)
    {
        if (_disposed || source is null) return;
        var generation = ++_generation;
        CancelLoading();
        _source = source;
        DisplayPath = source.DisplayPath;
        SizeLabel = source.SizeLabel;
        Dimensions = string.Empty;
        SetImage(null);
        Status = WorkspaceText.Get("AttachmentPreviewLoading");
        IsOpen = true;
        IsLoading = true;
        using var timeout = new CancellationTokenSource(_timeout, _timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken, timeout.Token);
        _loading = cancellation;
        try
        {
            var attachment = await source.LoadImageAsync(cancellation.Token).WaitAsync(cancellation.Token);
            var bitmap = await DecodeAsync(attachment, cancellation.Token);
            if (!IsCurrent(generation, source, cancellation)) { bitmap.Dispose(); return; }
            SetImage(bitmap);
            Dimensions = string.Format(CultureInfo.CurrentCulture,
                WorkspaceText.Get("AttachmentPreviewDimensionsFormat"), attachment.Width, attachment.Height);
            Status = Dimensions;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (IsOwnerCurrent(generation, source, cancellation))
                Status = WorkspaceText.Get(timeout.IsCancellationRequested
                    ? "AttachmentPreviewTimedOut" : "AttachmentPreviewCancelled");
        }
        catch (Exception)
        {
            if (IsOwnerCurrent(generation, source, cancellation))
                Status = WorkspaceText.Get("AttachmentPreviewFailed");
        }
        finally
        {
            if (ReferenceEquals(_loading, cancellation))
            {
                _loading = null;
                IsLoading = false;
                NotifyZoom();
            }
        }
    }

    private static async Task<Bitmap> DecodeAsync(AttachmentImage image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var bytes = Convert.FromBase64String(image.Base64Data);
        if (bytes.Length != image.ByteCount || bytes.Length is 0 or > AttachmentImage.MaximumByteCount
            || AttachmentImage.DetectMediaType(bytes) != image.MediaType
            || image.Width is < 1 or > AttachmentImage.MaximumDimension
            || image.Height is < 1 or > AttachmentImage.MaximumDimension
            || (long)image.Width * image.Height > AttachmentImage.MaximumPixels)
            throw new InvalidDataException("Invalid attachment preview snapshot.");
        if (Dispatcher.UIThread.CheckAccess()) return CreateBitmap(bytes, image, token);
        return await Dispatcher.UIThread.InvokeAsync(() => CreateBitmap(bytes, image, token),
            DispatcherPriority.Render, token);
    }

    private static Bitmap CreateBitmap(byte[] bytes, AttachmentImage image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var stream = new MemoryStream(bytes, writable: false);
        var bitmap = new Bitmap(stream);
        if (bitmap.PixelSize.Width == image.Width && bitmap.PixelSize.Height == image.Height) return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException("Attachment preview dimensions did not match its snapshot.");
    }

    private bool IsOwnerCurrent(int generation, AttachmentPreviewSource source, CancellationTokenSource loading) =>
        !_disposed && generation == _generation && IsOpen && ReferenceEquals(_source, source)
        && ReferenceEquals(_loading, loading);

    private bool IsCurrent(int generation, AttachmentPreviewSource source, CancellationTokenSource loading) =>
        IsOwnerCurrent(generation, source, loading) && !loading.IsCancellationRequested;

    private void SetZoom(double scale)
    {
        if (!CanZoom) return;
        Scale = scale;
        IsFit = false;
        NotifyZoom();
    }

    private void SetImage(Bitmap? bitmap)
    {
        if (ReferenceEquals(_image, bitmap)) return;
        var previous = _image;
        _image = bitmap;
        OnPropertyChanged(nameof(Image));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(IsStateVisible));
        previous?.Dispose();
        IsFit = true;
        Scale = 1;
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

    public void Close()
    {
        if (!IsOpen) return;
        _generation++;
        CancelLoading();
        IsOpen = false;
        IsLoading = false;
        _source = null;
        SetImage(null);
        DisplayPath = string.Empty;
        SizeLabel = string.Empty;
        Dimensions = string.Empty;
        Status = string.Empty;
        OnPropertyChanged(nameof(Title));
    }

    private void CancelLoading()
    {
        var loading = _loading;
        _loading = null;
        loading?.Cancel();
        loading?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Close();
        _disposed = true;
        _openCommand.Cancel();
    }
}
