using System.Security.Cryptography;

namespace DepalAdmin.Feature.Auth;

/// <summary>
/// 비밀번호를 그대로 저장하지 않고 PBKDF2(SHA-256, 10만 번 반복) 해시로 저장한다.
/// 사람마다 다른 소금값(salt)을 섞어서 같은 비밀번호라도 저장값이 달라진다.
/// </summary>
internal static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        try
        {
            var expected = Convert.FromBase64String(hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), Iterations,
                                                   HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);   // 비교 시간으로 힌트를 주지 않음
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>초기화용 임시 비밀번호 (헷갈리는 글자 0/O, 1/l 제외)</summary>
    public static string TemporaryPassword(int length = 8)
    {
        const string chars = "abcdefghjkmnpqrstuvwxyz23456789";
        return string.Concat(Enumerable.Range(0, length).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]));
    }
}
