using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class SourceAccountItemViewModel : ObservableObject
{
    private string _status;

    public SourceAccountItemViewModel(SourceAccount account)
    {
        Id = account.Id;
        Revision = account.Revision;
        Name = account.Name;
        Endpoint = account.ApiEndpoint.AbsoluteUri;
        IsEnabled = account.IsEnabled;
        _status = SourceAccountText.Get(IsEnabled ? "Unverified" : "Disabled");
    }

    public Guid Id { get; }
    public long Revision { get; }
    public string Name { get; }
    public string Endpoint { get; }
    public bool IsEnabled { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    internal void SetIdentity(SourceAccountIdentity identity) => Status = string.Format(
        CultureInfo.CurrentUICulture, SourceAccountText.Get("Verified"), identity.Login);

    internal void SetFailure(string status) => Status = status;
}
