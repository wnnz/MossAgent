using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>本地插件包设置页：选择包预览→确认安装→启用/停用→两段确认卸载。
/// 所有包操作经 <see cref="IPluginPackageInstaller"/> 端口；安装器自行重校验，不信任预览。</summary>
public sealed class PluginPackagesViewModel : ObservableObject, IDisposable
{
    private readonly IPluginPackageInstaller _installer;
    private readonly IPluginPackagePreviewer _previewer;
    private readonly IFilePickerService _picker;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private string _status = string.Empty;
    private bool _hasStatusError;
    private PluginPackagePreviewViewModel? _pendingPreview;
    private Guid? _uninstallPendingId;

    public PluginPackagesViewModel(IPluginPackageInstaller installer,
        IPluginPackagePreviewer previewer, IFilePickerService picker)
    {
        _installer = installer;
        _previewer = previewer;
        _picker = picker;
        Items.CollectionChanged += HandleItemsChanged;
        PickPackageCommand = new GuardedAsyncRelayCommand(PickPackageAsync, () => CanOperate);
        ConfirmInstallCommand = new GuardedAsyncRelayCommand(ConfirmInstallAsync,
            () => CanOperate && PendingPreview is not null);
        CancelInstallCommand = new RelayCommand(CancelInstall, () => PendingPreview is not null);
        RefreshCommand = new GuardedAsyncRelayCommand(RefreshAsync, () => CanOperate);
        ToggleEnabledCommand = new GuardedAsyncRelayCommand<InstalledPluginRowViewModel>(
            ToggleEnabledAsync, row => CanOperate && row is not null);
        UninstallCommand = new GuardedAsyncRelayCommand<InstalledPluginRowViewModel>(
            UninstallAsync, row => CanOperate && row is not null);
        foreach (var command in Operations) command.PropertyChanged += HandleOperationChanged;
        _ = LoadSafelyAsync();
    }

    public ObservableCollection<InstalledPluginRowViewModel> Items { get; } = [];
    public bool HasNoItems => Items.Count == 0;
    public bool CanOperate => !_disposed && !PickPackageCommand.IsRunning && !ConfirmInstallCommand.IsRunning
        && !RefreshCommand.IsRunning && !ToggleEnabledCommand.IsRunning && !UninstallCommand.IsRunning;
    public IAsyncRelayCommand PickPackageCommand { get; }
    public IAsyncRelayCommand ConfirmInstallCommand { get; }
    public IRelayCommand CancelInstallCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<InstalledPluginRowViewModel> ToggleEnabledCommand { get; }
    public IAsyncRelayCommand<InstalledPluginRowViewModel> UninstallCommand { get; }
    public bool HasPendingPreview => PendingPreview is not null;

    public PluginPackagePreviewViewModel? PendingPreview
    {
        get => _pendingPreview;
        private set
        {
            if (_disposed || !SetProperty(ref _pendingPreview, value)) return;
            OnPropertyChanged(nameof(HasPendingPreview));
            ConfirmInstallCommand.NotifyCanExecuteChanged();
            CancelInstallCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public bool HasStatusError { get => _hasStatusError; private set => SetProperty(ref _hasStatusError, value); }

    public string Status
    {
        get => _status;
        private set
        {
            if (!_disposed && SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    private IAsyncRelayCommand[] Operations =>
        [PickPackageCommand, ConfirmInstallCommand, RefreshCommand, ToggleEnabledCommand, UninstallCommand];

    private async Task PickPackageAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            var paths = await _picker.PickPluginPackageAsync(operation.Token);
            var path = paths.FirstOrDefault();
            if (path is null || _disposed) return;
            var preview = await _previewer.PreviewAsync(path, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            PendingPreview = new PluginPackagePreviewViewModel(preview, path);
            SetUninstallPending(null);
            SetStatus(Format("PluginPreviewReady", preview.Name, preview.Version));
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("PluginOperationCancelled"));
        }
        catch (PluginPackageException error) { SetStatus(MapPackageError(error.Code, "PluginPreviewFailed"), true); }
        catch (Exception) { SetStatus(SettingsText.Get("PluginPreviewFailed"), true); }
    }

    private async Task ConfirmInstallAsync(CancellationToken cancellationToken)
    {
        if (_disposed || PendingPreview is not { } pending) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            var installed = await _installer.InstallAsync(pending.PackagePath, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            PendingPreview = null;
            if (await TryLoadAsync(operation.Token) && !_disposed)
                SetStatus(Format("PluginInstalled", installed.Name, installed.Version));
            else
                SetStatus(SettingsText.Get("PluginInstalledRefreshFailed"), true);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("PluginOperationCancelled"));
        }
        catch (PluginPackageException error) { SetStatus(MapPackageError(error.Code, "PluginInstallFailed"), true); }
        catch (Exception) { SetStatus(SettingsText.Get("PluginInstallFailed"), true); }
    }

    private void CancelInstall()
    {
        if (_disposed || PendingPreview is null) return;
        PendingPreview = null;
        SetStatus(SettingsText.Get("PluginInstallCancelled"));
    }

    private async Task ToggleEnabledAsync(InstalledPluginRowViewModel? row, CancellationToken cancellationToken)
    {
        if (_disposed || row is null) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            var updated = row.IsEnabled
                ? await _installer.DisableAsync(row.Id, operation.Token)
                : await _installer.EnableAsync(row.Id, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            if (updated is null)
            {
                SetStatus(SettingsText.Get("PluginTargetUnavailable"), true);
                _ = await TryLoadAsync(operation.Token);
                return;
            }
            row.Update(updated);
            SetStatus(Format(updated.State == InstalledPluginState.Enabled ? "PluginEnabled" : "PluginDisabled", updated.Name));
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("PluginOperationCancelled"));
        }
        catch (Exception) { SetStatus(SettingsText.Get("PluginToggleFailed"), true); }
    }

    private async Task UninstallAsync(InstalledPluginRowViewModel? row, CancellationToken cancellationToken)
    {
        if (_disposed || row is null) return;
        if (_uninstallPendingId != row.Id)
        {
            SetUninstallPending(row.Id);
            SetStatus(Format("PluginUninstallPrompt", SettingsText.Get("PluginUninstallConfirm"), row.Name));
            return;
        }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            var outcome = await _installer.UninstallAsync(row.Id, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (_disposed) return;
            Items.Remove(row);
            SetUninstallPending(null);
            if (outcome is null)
            {
                SetStatus(SettingsText.Get("PluginTargetUnavailable"), true);
                return;
            }
            SetStatus(outcome.PayloadRemoved
                ? Format("PluginUninstalled", outcome.Plugin.Name)
                : Format("PluginUninstallPayloadKept", outcome.Plugin.Name));
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("PluginOperationCancelled"));
        }
        catch (Exception) { SetStatus(SettingsText.Get("PluginUninstallFailed"), true); }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        try
        {
            await LoadAsync(operation.Token);
            if (_disposed) return;
            SetStatus(SettingsText.Get("PluginListRefreshed"));
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            SetStatus(SettingsText.Get("PluginOperationCancelled"));
        }
        catch (Exception) { SetStatus(SettingsText.Get("PluginLoadFailed"), true); }
    }

    private async Task LoadAsync(CancellationToken token)
    {
        var plugins = await _installer.ListAsync(token);
        token.ThrowIfCancellationRequested();
        if (_disposed) return;
        Items.Clear();
        foreach (var plugin in plugins)
        {
            Items.Add(new InstalledPluginRowViewModel(plugin) { IsUninstallPending = plugin.Id == _uninstallPendingId });
        }
    }

    private async Task<bool> TryLoadAsync(CancellationToken token)
    {
        try { await LoadAsync(token); return true; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private async Task LoadSafelyAsync()
    {
        try { await LoadAsync(_lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception) { SetStatus(SettingsText.Get("PluginLoadFailed"), true); }
    }

    private void SetUninstallPending(Guid? id)
    {
        _uninstallPendingId = id;
        foreach (var item in Items) item.IsUninstallPending = item.Id == id;
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (_disposed) return;
        Status = message;
        HasStatusError = isError;
    }

    private static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, SettingsText.Get(key), args);

    private static string MapPackageError(string code, string fallbackKey) => code switch
    {
        PluginPackageErrorCode.Unreadable => SettingsText.Get("PluginErrorUnreadable"),
        PluginPackageErrorCode.EntryInvalid => SettingsText.Get("PluginErrorEntryInvalid"),
        PluginPackageErrorCode.TooLarge => SettingsText.Get("PluginErrorTooLarge"),
        PluginPackageErrorCode.ManifestMissing => SettingsText.Get("PluginErrorManifestMissing"),
        PluginPackageErrorCode.ManifestInvalid => SettingsText.Get("PluginErrorManifestInvalid"),
        PluginPackageErrorCode.McpInvalid => SettingsText.Get("PluginErrorMcpInvalid"),
        PluginPackageErrorCode.InstallConflict => SettingsText.Get("PluginErrorConflict"),
        PluginPackageErrorCode.InstallFailed => SettingsText.Get("PluginInstallFailed"),
        _ => SettingsText.Get(fallbackKey)
    };

    private void HandleItemsChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        OnPropertyChanged(nameof(HasNoItems));

    private void HandleOperationChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || args.PropertyName != nameof(IAsyncRelayCommand.IsRunning)) return;
        OnPropertyChanged(nameof(CanOperate));
        foreach (var command in Operations) command.NotifyCanExecuteChanged();
        CancelInstallCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Items.CollectionChanged -= HandleItemsChanged;
        foreach (var command in Operations)
        {
            command.PropertyChanged -= HandleOperationChanged;
            ((IDisposable)command).Dispose();
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
