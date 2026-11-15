using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace WineSite;

public class User
{
    private const int HashIterations = 100_000;

    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";

    /// <summary>Plain-text password from data files written before hashing; migrated on load and never written back.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }

    public void SetPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        PasswordSalt = Convert.ToBase64String(salt);
        PasswordHash = Convert.ToBase64String(Hash(password, salt));
        Password = null;
    }

    public bool CheckPassword(string password) =>
        PasswordSalt != "" && PasswordHash != "" &&
        CryptographicOperations.FixedTimeEquals(
            Hash(password, Convert.FromBase64String(PasswordSalt)),
            Convert.FromBase64String(PasswordHash));

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, HashIterations, HashAlgorithmName.SHA256, 32);
}

public class Booking
{
    public static readonly string[] EventTypes =
    [
        "Среща за влюбени",
        "Вечер по женски",
        "Събитие с приятели",
        "Семейно събитие",
        "За малките художници",
        "Работна среща",
    ];

    public int Id { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string EventType { get; set; } = "";

    /// <summary>Local start time of the event, e.g. 2026-12-05 18:00.</summary>
    public DateTime Start { get; set; }

    public string Notes { get; set; } = "";
}
