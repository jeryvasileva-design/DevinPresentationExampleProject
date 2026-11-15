using System.Text.Encodings.Web;
using System.Text.Json;

namespace WineSite;

/// <summary>Keeps users and bookings in memory and writes them to JSON files in the data folder.</summary>
public class Store
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _dir;

    public object Lock { get; } = new();
    public List<User> Users { get; } = new();
    public List<Booking> Bookings { get; } = new();

    public Store(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(_dir);
        SeedDemoData();
    }

    public int NextUserId() => Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;

    public int NextBookingId() => Bookings.Count == 0 ? 1 : Bookings.Max(b => b.Id) + 1;

    public void Save()
    {
        File.WriteAllText(Path.Combine(_dir, "users.json"), JsonSerializer.Serialize(Users, JsonOptions));
        File.WriteAllText(Path.Combine(_dir, "bookings.json"), JsonSerializer.Serialize(Bookings, JsonOptions));
    }

    /// <summary>Two demo users, each with two future events, so the calendar is not empty.</summary>
    private void SeedDemoData()
    {
        var today = DateTime.Today;

        Users.Add(new User { Id = 1, UserName = "Мария Иванова", Email = "maria@example.com", Password = "123456" });
        Users.Add(new User { Id = 2, UserName = "Иван Петров", Email = "ivan@example.com", Password = "123456" });

        Bookings.Add(new Booking
        {
            Id = 1, UserId = 1, FirstName = "Мария", LastName = "Иванова", Phone = "0888123456",
            EventType = "Среща за влюбени", Start = today.AddDays(4).AddHours(18),
        });
        Bookings.Add(new Booking
        {
            Id = 2, UserId = 1, FirstName = "Мария", LastName = "Иванова", Phone = "0888123456",
            EventType = "Вечер по женски", Start = today.AddDays(10).AddHours(19),
        });
        Bookings.Add(new Booking
        {
            Id = 3, UserId = 2, FirstName = "Иван", LastName = "Петров", Phone = "0899654321",
            EventType = "Работна среща", Start = today.AddDays(6).AddHours(17),
        });
        Bookings.Add(new Booking
        {
            Id = 4, UserId = 2, FirstName = "Иван", LastName = "Петров", Phone = "0899654321",
            EventType = "Семейно събитие", Start = today.AddDays(12).AddHours(11),
        });
    }
}
