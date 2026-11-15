using System.Globalization;
using WineSite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(new Store(Path.Combine(builder.Environment.ContentRootPath, "data")));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

static bool TryParseStart(string date, string time, out DateTime start) =>
    DateTime.TryParseExact($"{date} {time}", "yyyy-MM-dd HH:mm:ss",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out start);

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

    lock (store.Lock)
    {
        store.Users.Add(new User
        {
            Id = store.NextUserId(),
            UserName = name,
            Email = email,
            Password = password,
        });
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
        user = store.Users.FirstOrDefault(u => u.Email == email && u.Password == password);
    }

    if (user == null)
    {
        return Results.Json(new { status = "Error", message = "Грешен имейл или парола." }, statusCode: 401);
    }

    return Results.Json(new { status = "Success", id = user.Id, name = user.UserName });
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

    Booking booking;
    lock (store.Lock)
    {
        booking = new Booking
        {
            Id = store.NextBookingId(),
            UserId = userId,
            FirstName = form["firstName"].ToString().Trim(),
            LastName = form["lastName"].ToString().Trim(),
            Phone = form["phone"].ToString().Trim(),
            EventType = form["eventType"].ToString(),
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

    if (!DateTime.TryParseExact(form["date"].ToString(), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
        return Results.Json(new { status = "Error", message = "Невалидна дата." });
    }

    var title = form["title"].ToString();

    lock (store.Lock)
    {
        var booking = store.Bookings.FirstOrDefault(b => b.Id == id);
        if (booking == null)
        {
            return Results.Json(new { status = "Error", message = "Събитието не е намерено." });
        }

        booking.Start = date.Date + booking.Start.TimeOfDay;
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
        if (id >= 1 && id <= store.Bookings.Count)
        {
            store.Bookings.RemoveAt(id - 1);
            store.Save();
        }
    }

    return Results.Json(new { status = "Success" });
});

app.Run();
