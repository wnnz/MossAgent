using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MossAgent.Application.Attachments;
using MossAgent.App.Services;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed class AppAttachmentThumbnail : Image
{
    public static readonly StyledProperty<AttachmentPreviewSource?> ThumbnailProperty =
        AvaloniaProperty.Register<AppAttachmentThumbnail, AttachmentPreviewSource?>(nameof(Thumbnail));
    public static readonly DirectProperty<AppAttachmentThumbnail, bool> HasThumbnailProperty =
        AvaloniaProperty.RegisterDirect<AppAttachmentThumbnail, bool>(nameof(HasThumbnail), control => control.HasThumbnail);
    private CancellationTokenSource? _read;
    private Bitmap? _bitmap;
    private bool _attached;

    protected override Type StyleKeyOverride => typeof(Image);
    public bool HasThumbnail => _bitmap is not null;
    internal Task Loading { get; private set; } = Task.CompletedTask;

    public AttachmentPreviewSource? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        StartLoad();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        StopLoad();
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ThumbnailProperty) StartLoad();
    }

    private void StartLoad()
    {
        StopLoad();
        if (!_attached || Thumbnail is not { } source) return;
        var read = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _read = read;
        Loading = LoadAsync(source, read, read.Token);
    }

    private async Task LoadAsync(AttachmentPreviewSource source, CancellationTokenSource read, CancellationToken token)
    {
        try
        {
            var image = await source.LoadAsync(token).WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (image.MediaType != "image/png" || image.ByteCount > IAttachmentThumbnailRenderer.MaximumByteCount
                || image.Width > IAttachmentThumbnailRenderer.MaximumDimension
                || image.Height > IAttachmentThumbnailRenderer.MaximumDimension) return;
            Dispatcher.UIThread.Post(() => Publish(image, read, token));
        }
        catch (Exception) { }
    }

    private void Publish(AttachmentImage image, CancellationTokenSource read, CancellationToken token)
    {
        if (!_attached || token.IsCancellationRequested || !ReferenceEquals(_read, read)) return;
        try
        {
            var bytes = Convert.FromBase64String(image.Base64Data);
            if (bytes.Length < 24 || AttachmentImage.DetectMediaType(bytes) != "image/png"
                || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != image.Width
                || BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != image.Height) return;
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width > IAttachmentThumbnailRenderer.MaximumDimension
                || bitmap.PixelSize.Height > IAttachmentThumbnailRenderer.MaximumDimension)
            {
                bitmap.Dispose();
                return;
            }
            SetBitmap(bitmap);
        }
        catch (Exception) { }
    }

    private void StopLoad()
    {
        var read = _read;
        _read = null;
        read?.Cancel();
        read?.Dispose();
        SetBitmap(null);
    }

    private void SetBitmap(Bitmap? bitmap)
    {
        var before = HasThumbnail;
        Source = bitmap;
        var previous = _bitmap;
        _bitmap = bitmap;
        previous?.Dispose();
        if (before != HasThumbnail) RaisePropertyChanged(HasThumbnailProperty, before, HasThumbnail);
    }
}
