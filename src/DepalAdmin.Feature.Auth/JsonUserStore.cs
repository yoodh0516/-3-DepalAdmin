using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DepalAdmin.Core.Auth;
using DepalAdmin.Core.Settings;

namespace DepalAdmin.Feature.Auth;

/// <summary>
/// 계정을 data/users.json에 저장한다.
/// 처음 실행하면 관리자 계정(admin / admin1234)을 만들고, 첫 로그인 때 비밀번호를 바꾸게 한다.
///
/// TODO(장성찬 + DB 담당): 팀 PC 여러 대에서 같은 계정을 쓰려면 DB 담당과 users 테이블을 정해서
///                   MySQL 저장으로 바꾸는 방법을 검토 (이 클래스만 바꾸면 됨).
/// </summary>
public sealed class JsonUserStore
{
    public const string DefaultAdminId = "admin";
    public const string DefaultAdminPassword = "admin1234";
    public const int MinPasswordLength = 6;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly List<UserAccount> _users;

    public JsonUserStore(AppSettings settings)
    {
        _path = settings.DataPath("users.json");
        _users = Load();
        if (_users.Count == 0)
        {
            Add(DefaultAdminId, "관리자", Role.Admin, DefaultAdminPassword, mustChangePassword: true);
        }
    }

    public IReadOnlyList<UserAccount> All => _users.OrderBy(u => u.Id).ToList();

    public UserAccount? Find(string id) =>
        _users.FirstOrDefault(u => string.Equals(u.Id, id.Trim(), StringComparison.OrdinalIgnoreCase));

    public int ActiveAdminCount => _users.Count(u => u.Role == Role.Admin && !u.Disabled);

    /// <summary>문제가 있으면 이유를 담은 InvalidOperationException</summary>
    public UserAccount Add(string id, string name, Role role, string password, bool mustChangePassword)
    {
        id = id.Trim();
        if (id.Length < 3 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new InvalidOperationException("아이디는 영문·숫자·_ 로 3자 이상이어야 합니다.");
        if (Find(id) != null)
            throw new InvalidOperationException($"'{id}' 아이디가 이미 있습니다.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("이름을 입력하세요.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new UserAccount
        {
            Id = id,
            Name = name.Trim(),
            Role = role,
            PasswordHash = hash,
            Salt = salt,
            MustChangePassword = mustChangePassword,
        };
        _users.Add(user);
        Save();
        return user;
    }

    public bool CheckPassword(UserAccount user, string password) =>
        PasswordHasher.Verify(password, user.PasswordHash, user.Salt);

    public void SetPassword(UserAccount user, string password, bool mustChangePassword)
    {
        (user.PasswordHash, user.Salt) = PasswordHasher.Hash(password);
        user.MustChangePassword = mustChangePassword;
        Save();
    }

    /// <summary>역할·사용 여부·마지막 로그인 등을 바꾼 뒤 부른다</summary>
    public void Update(UserAccount user) => Save();

    public void Delete(UserAccount user)
    {
        _users.Remove(user);
        Save();
    }

    private List<UserAccount> Load()
    {
        if (!File.Exists(_path)) return new List<UserAccount>();
        return JsonSerializer.Deserialize<List<UserAccount>>(File.ReadAllText(_path), Options) ?? new List<UserAccount>();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_users, Options));
    }
}
