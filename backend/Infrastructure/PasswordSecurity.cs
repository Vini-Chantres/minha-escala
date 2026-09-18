using System.Security.Cryptography;
using System.Text;

namespace MinhaEscala.Infrastructure;

public static class PasswordSecurity
{
    private const int Iterations = 600_000;
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string encoded)
    {
        try
        {
            var p = encoded.Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2-sha256" || !int.TryParse(p[1], out var iterations) || iterations < 210_000 || iterations > 1_000_000) return false;
            var salt = Convert.FromBase64String(p[2]); var expected = Convert.FromBase64String(p[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected, Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
    public static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
