namespace WineSite;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class Booking
{
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
