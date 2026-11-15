using System.Globalization;
using System.Text.RegularExpressions;
using WineSite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Store(Path.Combine(builder.Environment.ContentRootPath, "data")));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

static bool TryParseStart(string date, string time, out DateTime start) =>
    DateTime.TryParseExact($"{date} {time}", ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm"],
        CultureInfo.InvariantCulture, DateTimeStyles.None, out start);

// Rules 4 and 6: the server checks the start time itself, whatever the browser sent.
static string? CheckStart(DateTime start)
{
    if (start <= DateTime.Now)
    {
        return "Датата и часът не може да са в миналото.";
    }

    if (start.Minute != 0 || start.Second != 0 || start.Hour < 9 || start.Hour > 21)
    {
        return "Часът трябва да е кръгъл, между 09:00 и 21:00.";
    }

    return null;
}

static string? CheckEventType(string eventType) =>
    Booking.EventTypes.Contains(eventType) ? null : "Изберете вид събитие от списъка.";

// One hour = one event, for all users. The booking being edited does not count.
static bool IsTaken(Store store, DateTime start, int? exceptBookingId = null) =>
    store.Bookings.Any(b => b.Start == start && b.Id != exceptBookingId);

const string TakenMessage = "Този час е зает.";

// ---------- registration ----------
app.MapPost("/api/register", async (HttpRequest req, Store store) =>
{
    var form = await req.ReadFormAsync();
    var name = form["userName"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();

    if (name == "" || email == "" || password.Length < 6)
    {
        return Results.Text("Попълнете всички полета (паролата е поне 6 символа).");
    }

    var user = new User { UserName = name, Email = email };
    user.SetPassword(password);

    lock (store.Lock)
    {
        if (store.FindUserByEmail(email) != null)
        {
            return Results.Text("Този имейл вече е регистриран.");
        }

        user.Id = store.NextUserId();
        store.Users.Add(user);
        store.Save();
    }

    return Results.Text("Success");
});

// ---------- login ----------
app.MapPost("/api/login", async (HttpRequest req, Store store) =>
{
    var form = await req.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();

    User? user;
    lock (store.Lock)
    {
        user = store.FindUserByEmail(email);
    }

    if (user == null || !user.CheckPassword(password))
    {
        return Results.Json(new { status = "Error", message = "Грешен имейл или парола." }, statusCode: 401);
    }

    return Results.Json(new { status = "Success", id = user.Id, userName = user.UserName });
});

// ---------- save an hour (book an event) ----------
app.MapPost("/api/savehour", async (HttpRequest req, Store store) =>
{
    var form = await req.ReadFormAsync();

    if (!int.TryParse(form["userId"].ToString(), out var userId))
    {
        return Results.Json(new { status = "Error", message = "Трябва да сте влезли в профила си." });
    }

    if (!TryParseStart(form["eventDate"].ToString(), form["eventTime"].ToString(), out var start))
    {
        return Results.Json(new { status = "Error", message = "Невалидна дата или час." });
    }

    var firstName = form["firstName"].ToString().Trim();
    var lastName = form["lastName"].ToString().Trim();
    var phone = form["phone"].ToString().Trim();
    var eventType = form["eventType"].ToString().Trim();

    var error = CheckStart(start)
        ?? (Regex.IsMatch(phone, "^0[0-9]{9}$") ? null : "Телефонът трябва да е 10 цифри и да започва с 0.")
        ?? (firstName != "" && lastName != "" ? null : "Попълнете име и фамилия.")
        ?? CheckEventType(eventType);
    if (error != null)
    {
        return Results.Json(new { status = "Error", message = error });
    }

    Booking booking;
    lock (store.Lock)
    {
        if (!store.Users.Any(u => u.Id == userId))
        {
            return Results.Json(new { status = "Error", message = "Трябва да сте влезли в профила си." });
        }

        if (IsTaken(store, start))
        {
            return Results.Json(new { status = "Error", message = TakenMessage });
        }

        booking = new Booking
        {
            Id = store.NextBookingId(),
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            EventType = eventType,
            Start = start,
            Notes = form["notes"].ToString(),
        };
        store.Bookings.Add(booking);
        store.Save();
    }

    return Results.Json(new { status = "Success", id = booking.Id });
});

// ---------- events for the calendar (FullCalendar) ----------
app.MapGet("/api/events", (int userId, Store store) =>
{
    lock (store.Lock)
    {
        var events = store.Bookings
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Start)
            .Select(b => new
            {
                id = b.Id,
                title = b.EventType,
                start = b.Start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            })
            .ToList();

        return Results.Json(events);
    }
});

// ---------- edit an event ----------
app.MapPost("/api/events/update", async (HttpRequest req, Store store) =>
{
    var form = await req.ReadFormAsync();

    if (!int.TryParse(form["id"].ToString(), out var id))
    {
        return Results.Json(new { status = "Error", message = "Невалидно събитие." });
    }

    if (!TryParseStart(form["date"].ToString(), form["time"].ToString(), out var start))
    {
        return Results.Json(new { status = "Error", message = "Невалидна дата или час." });
    }

    var title = form["title"].ToString().Trim();

    var error = CheckEventType(title) ?? CheckStart(start);
    if (error != null)
    {
        return Results.Json(new { status = "Error", message = error });
    }

    lock (store.Lock)
    {
        var booking = store.Bookings.FirstOrDefault(b => b.Id == id);
        if (booking == null)
        {
            return Results.Json(new { status = "Error", message = "Събитието не е намерено." });
        }

        if (IsTaken(store, start, booking.Id))
        {
            return Results.Json(new { status = "Error", message = TakenMessage });
        }

        booking.Start = start;
        booking.EventType = title;
        store.Save();
    }

    return Results.Json(new { status = "Success" });
});

// ---------- delete an event ----------
app.MapPost("/api/events/delete", async (HttpRequest req, Store store) =>
{
    var form = await req.ReadFormAsync();

    if (!int.TryParse(form["id"].ToString(), out var id))
    {
        return Results.Json(new { status = "Error", message = "Невалидно събитие." });
    }

    lock (store.Lock)
    {
        if (store.Bookings.RemoveAll(b => b.Id == id) == 0)
        {
            return Results.Json(new { status = "Error", message = "Събитието не е намерено." });
        }

        store.Save();
    }

    return Results.Json(new { status = "Success" });
});

app.Run();
