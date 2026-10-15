using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class ToolApprovalContentViewModel : ObservableObject
{
    private const int PageSize = 4096;
    private readonly JsonElement _arguments;
    private readonly string[] _properties;
    private readonly Stack<int> _previousOffsets = [];
    private string _text = string.Empty;
    private int _offset;
    private bool _isOpen;

    public ToolApprovalContentViewModel(string toolName, JsonElement arguments)
    {
        _properties = toolName switch
        {
            "filesystem.write_file" => ["content"],
            "filesystem.replace_text" => ["oldText", "newText"],
            "shell.run" => ["command"],
            "terminal.write" or "browser.type" => ["text"],
            _ => []
        };
        _arguments = _properties.Length > 0 ? arguments.Clone() : default;
        IsAvailable = _arguments.ValueKind == JsonValueKind.Object
            && _properties.Any(property => _arguments.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String);
        ToggleCommand = new RelayCommand(Toggle, () => IsAvailable);
        NextCommand = new RelayCommand(Next, () => IsOpen && _offset + PageLength < _text.Length);
        PreviousCommand = new RelayCommand(Previous, () => IsOpen && _previousOffsets.Count > 0);
    }

    public IRelayCommand ToggleCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public bool IsAvailable { get; }
    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }
    public bool HasMultiplePages => IsOpen && (_previousOffsets.Count > 0 || PageLength < _text.Length);
    public string ToggleLabel => ApprovalText.Get(IsOpen ? "HideContent" : "ShowContent");
    public string PageText => IsOpen ? VisibleText(_text.AsSpan(_offset, PageLength)) : string.Empty;
    public string RangeLabel => IsOpen
        ? ApprovalText.Format("ContentRange", _offset + 1, _offset + PageLength, _text.Length) : string.Empty;

    private int PageLength
    {
        get
        {
            var length = Math.Min(PageSize, _text.Length - _offset);
            if (length > 0 && _offset + length < _text.Length && char.IsHighSurrogate(_text[_offset + length - 1]))
                length--;
            return length;
        }
    }

    private void Toggle()
    {
        if (!IsAvailable) return;
        if (IsOpen) { Close(); return; }
        var sections = new List<string>();
        foreach (var property in _properties)
        {
            if (!_arguments.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) continue;
            var label = property switch
            {
                "content" => "ContentBody", "oldText" => "OldTextBody", "newText" => "NewTextBody",
                "command" => "CommandBody", _ => "InputBody"
            };
            var content = value.GetString()!;
            sections.Add(ApprovalText.Get(label) + Environment.NewLine
                + (content.Length == 0 ? ApprovalText.Get("EmptyContent") : content));
        }
        _text = string.Join(Environment.NewLine + Environment.NewLine, sections);
        _offset = 0;
        IsOpen = true;
        Refresh();
    }

    public void Close()
    {
        IsOpen = false;
        _text = string.Empty;
        _offset = 0;
        _previousOffsets.Clear();
        Refresh();
    }

    private void Next()
    {
        if (!NextCommand.CanExecute(null)) return;
        _previousOffsets.Push(_offset);
        _offset += PageLength;
        Refresh();
    }

    private void Previous()
    {
        if (!PreviousCommand.CanExecute(null) || !_previousOffsets.TryPop(out _offset)) return;
        Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(HasMultiplePages));
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }

    private static string VisibleText(ReadOnlySpan<char> value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is not ('\r' or '\n' or '\t') && (char.IsControl(character)
                || char.GetUnicodeCategory(character) is UnicodeCategory.Format
                    or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
                text.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
            else text.Append(character);
        }
        return text.ToString();
    }
}
