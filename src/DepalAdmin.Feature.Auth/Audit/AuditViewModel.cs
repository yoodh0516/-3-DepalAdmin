using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Export;

namespace DepalAdmin.Feature.Auth.Audit;

/// <summary>
/// 누가 언제 무엇을 했는지 (로그인, 라인 명령, 비상정지, 알람 확인, 계정 변경, 설정 변경 …).
/// </summary>
public partial class AuditViewModel : ObservableObject
{
    private const string All = "전체";
    private readonly IAuditLog _audit;
    private List<AuditEntry> _loaded = new();

    public ObservableCollection<AuditEntry> Rows { get; } = new();
    public ObservableCollection<string> Categories { get; } = new() { All };

    [ObservableProperty] private DateTime _from = DateTime.Today.AddDays(-6);
    [ObservableProperty] private DateTime _to = DateTime.Today;
    [ObservableProperty] private string _category = All;
    [ObservableProperty] private string _userText = "";
    [ObservableProperty] private string _status = "";

    public AuditViewModel(IAuditLog audit) => _audit = audit;

    partial void OnCategoryChanged(string value) => ApplyFilter();
    partial void OnUserTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task LoadAsync()
    {
        Status = "읽는 중…";
        _loaded = (await _audit.ReadAsync(From.Date, To.Date.AddDays(1))).ToList();

        var keep = Category;
        Categories.Clear();
        Categories.Add(All);
        foreach (var c in _loaded.Select(e => e.Category).Distinct().OrderBy(c => c)) Categories.Add(c);
        Category = Categories.Contains(keep) ? keep : All;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        foreach (var e in _loaded.Where(e =>
                     (Category == All || e.Category == Category) &&
                     (string.IsNullOrWhiteSpace(UserText) ||
                      e.UserId.Contains(UserText, StringComparison.OrdinalIgnoreCase) ||
                      e.UserName.Contains(UserText, StringComparison.OrdinalIgnoreCase))))
        {
            Rows.Add(e);
        }
        Status = $"{Rows.Count}건";
    }

    [RelayCommand]
    private void Export()
    {
        var path = CsvExport.Save($"작업기록_{From:yyyyMMdd}-{To:yyyyMMdd}.csv", Rows,
            ("시각", e => e.Time),
            ("아이디", e => e.UserId),
            ("이름", e => e.UserName),
            ("종류", e => e.Category),
            ("내용", e => e.Message));
        if (path != null) Status = $"저장함: {path}";
    }
}
