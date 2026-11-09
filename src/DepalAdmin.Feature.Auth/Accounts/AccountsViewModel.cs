using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DepalAdmin.Core;
using DepalAdmin.Core.Auth;

namespace DepalAdmin.Feature.Auth.Accounts;

/// <summary>
/// 계정 관리 (관리자만).
/// 새 계정·비밀번호 초기화는 임시 비밀번호를 보여 주고, 그 사람이 첫 로그인 때 바꾸게 한다.
/// </summary>
public partial class AccountsViewModel : ObservableObject
{
    private readonly JsonUserStore _store;
    private readonly Session _session;
    private readonly IAuditLog _audit;

    public ObservableCollection<UserRow> Users { get; } = new();
    public IReadOnlyList<RoleOption> Roles { get; } = new[] { new RoleOption(Role.Viewer), new RoleOption(Role.Operator), new RoleOption(Role.Admin) };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyRoleCommand), nameof(ResetPasswordCommand), nameof(ToggleDisabledCommand), nameof(DeleteCommand))]
    private UserRow? _selected;

    [ObservableProperty] private Role _editRole;

    [ObservableProperty] private string _newId = "";
    [ObservableProperty] private string _newName = "";
    [ObservableProperty] private Role _newRole = Role.Operator;

    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isError;

    public AccountsViewModel(JsonUserStore store, Session session, IAuditLog audit)
    {
        _store = store;
        _session = session;
        _audit = audit;
        _session.Changed += (_, _) => Refresh();
        Refresh();
    }

    partial void OnSelectedChanged(UserRow? value)
    {
        if (value != null) EditRole = value.Account.Role;
    }

    private void Refresh()
    {
        var selectedId = Selected?.Id;
        Users.Clear();
        foreach (var user in _store.All) Users.Add(new UserRow(user, user.Id == _session.User?.Id));
        Selected = Users.FirstOrDefault(u => u.Id == selectedId);
    }

    private bool HasSelection() => Selected != null;

    [RelayCommand]
    private void Add()
    {
        try
        {
            var temp = PasswordHasher.TemporaryPassword();
            var user = _store.Add(NewId, NewName, NewRole, temp, mustChangePassword: true);
            _audit.Write("계정 추가", $"{user.Id} ({Labels.RoleName(user.Role)})");
            Show($"'{user.Id}' 계정을 만들었습니다. 임시 비밀번호: {temp}  — 첫 로그인 때 새 비밀번호를 정하게 됩니다.");
            NewId = "";
            NewName = "";
            Refresh();
        }
        catch (InvalidOperationException ex)
        {
            Show(ex.Message, error: true);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ApplyRole()
    {
        var user = Selected!.Account;
        if (user.Role == EditRole) return;
        if (Selected.IsMe && EditRole != Role.Admin)
        {
            Show("자기 계정의 관리자 권한은 내릴 수 없습니다. 다른 관리자에게 부탁하세요.", error: true);
            return;
        }
        if (user.Role == Role.Admin && _store.ActiveAdminCount <= 1)
        {
            Show("관리자가 최소 한 명은 있어야 합니다.", error: true);
            return;
        }

        var before = user.Role;
        user.Role = EditRole;
        _store.Update(user);
        _audit.Write("권한 변경", $"{user.Id}: {Labels.RoleName(before)} → {Labels.RoleName(user.Role)}");
        Show($"'{user.Id}' 권한을 {Labels.RoleName(user.Role)}(으)로 바꿨습니다.");
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ResetPassword()
    {
        var user = Selected!.Account;
        var temp = PasswordHasher.TemporaryPassword();
        _store.SetPassword(user, temp, mustChangePassword: true);
        _audit.Write("비밀번호 초기화", user.Id);
        Show($"'{user.Id}' 임시 비밀번호: {temp}  — 본인에게 전달하세요. 다음 로그인 때 바꾸게 됩니다.");
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleDisabled()
    {
        var row = Selected!;
        if (row.IsMe)
        {
            Show("자기 계정은 사용 중지할 수 없습니다.", error: true);
            return;
        }
        if (!row.Account.Disabled && row.Account.Role == Role.Admin && _store.ActiveAdminCount <= 1)
        {
            Show("관리자가 최소 한 명은 있어야 합니다.", error: true);
            return;
        }

        row.Account.Disabled = !row.Account.Disabled;
        _store.Update(row.Account);
        _audit.Write(row.Account.Disabled ? "계정 사용 중지" : "계정 다시 사용", row.Id);
        Show(row.Account.Disabled ? $"'{row.Id}' 계정을 사용 중지했습니다." : $"'{row.Id}' 계정을 다시 쓸 수 있습니다.");
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        var row = Selected!;
        if (row.IsMe)
        {
            Show("자기 계정은 지울 수 없습니다.", error: true);
            return;
        }
        if (row.Account.Role == Role.Admin && _store.ActiveAdminCount <= 1)
        {
            Show("관리자가 최소 한 명은 있어야 합니다.", error: true);
            return;
        }

        var answer = MessageBox.Show($"'{row.Id}' ({row.Name}) 계정을 지웁니다. 되돌릴 수 없습니다.\n\n작업 기록에 남은 이름은 그대로 남습니다.",
                                     "계정 삭제", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;

        _store.Delete(row.Account);
        _audit.Write("계정 삭제", row.Id);
        Show($"'{row.Id}' 계정을 지웠습니다.");
        Refresh();
    }

    private void Show(string message, bool error = false)
    {
        Message = message;
        IsError = error;
    }
}

public sealed record RoleOption(Role Role)
{
    public string Text => Labels.RoleName(Role);
}

public sealed class UserRow
{
    public UserRow(UserAccount account, bool isMe)
    {
        Account = account;
        IsMe = isMe;
    }

    public UserAccount Account { get; }
    public bool IsMe { get; }

    public string Id => Account.Id;
    public string Name => IsMe ? $"{Account.Name} (나)" : Account.Name;
    public string RoleText => Labels.RoleName(Account.Role);

    public string StateText => Account.Disabled ? "사용 중지"
                             : Account.MustChangePassword ? "비밀번호 바꿀 차례"
                             : "사용 중";

    /// <summary>StatusToBrush용: 중지는 주의색</summary>
    public string StateKey => Account.Disabled ? "warn" : "neutral";

    public string LastLogin => Account.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "없음";
}
