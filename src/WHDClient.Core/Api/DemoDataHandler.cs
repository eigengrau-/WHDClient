using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WHDClient.Core.Models;

namespace WHDClient.Core.Api;

/// <summary>
/// Demo mode (launch with WHD_DEMO=1): serves fabricated tickets, notes and lookups so the
/// app can be exercised and screenshotted without touching a real server or exposing real data.
/// Any API key and any server URL are accepted; nothing leaves the machine.
/// </summary>
public class DemoDataHandler : HttpMessageHandler
{
    public const string DemoServerUrl = "https://webhelpdesk.example.com";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(Respond(request));

    private static HttpResponseMessage Respond(HttpRequestMessage req)
    {
        var path = req.RequestUri!.AbsolutePath;
        var q = ParseQuery(req.RequestUri.Query);
        var ra = path.Contains("/ra/") ? path[(path.IndexOf("/ra/", StringComparison.Ordinal) + 3)..] : path;
        var method = req.Method.Method;

        object? payload = (method, ra) switch
        {
            ("GET", "/Techs/currentTech") => new Tech { Id = 1, Type = "Tech" },
            ("GET", var p) when Regex.IsMatch(p, @"^/Techs/\d+$") => DemoData.TechById(IdOf(p)),
            ("GET", "/Techs") => DemoData.Techs,
            ("GET", "/StatusTypes") => DemoData.StatusTypes,
            ("GET", "/PriorityTypes") => DemoData.PriorityTypes,
            ("GET", var p) when Regex.IsMatch(p, @"^/RequestTypes/\d+$") => DemoData.RequestTypeById(IdOf(p)),
            ("GET", "/RequestTypes") => DemoData.RequestTypes,
            ("GET", "/Locations") => DemoData.Locations,
            ("GET", "/Clients") => DemoData.Clients,
            ("GET", "/Session") => new WhdSession { Type = "Session", SessionKey = "demo-session", CurrentTechId = 1 },
            ("GET", "/TicketNotes") => DemoData.NotesFor(Int(q, "jobTicketId")),
            ("GET", "/Tickets/mine") => DemoData.Page(DemoData.Mine, q),
            ("GET", "/Tickets/group" or "/Tickets/flagged" or "/Tickets/recent") => DemoData.Page(DemoData.All, q),
            ("GET", var p) when Regex.IsMatch(p, @"^/Tickets/\d+$") => DemoData.WithEmbeddedNotes(DemoData.TicketById(IdOf(p))),
            ("GET", "/Tickets") => DemoData.Page(DemoData.Search(q.GetValueOrDefault("qualifier")), q),
            ("POST", "/Tickets") => DemoData.TicketById(1001),
            ("PUT", var p) when Regex.IsMatch(p, @"^/Tickets/\d+$") => DemoData.ApplyTicketUpdate(IdOf(p), ReadBody(req)),
            ("POST", "/TechNotes") => DemoData.NotesFor(1001)[0],
            _ => null
        };

        // Attachment download: tiny stand-in file with a proper filename header.
        if (method == "GET" && Regex.IsMatch(ra, @"^/TicketAttachments/\d+$"))
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent("Demo attachment content."u8.ToArray())
            };
            resp.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
            {
                FileName = "\"demo-attachment.txt\""
            };
            return resp;
        }
        // Multipart attachment upload.
        if (method == "POST" && path.EndsWith("/attachment/upload", StringComparison.Ordinal))
            payload = new TicketAttachment { Id = 900, Type = "TicketAttachment", FileName = "upload.bin", UploadDate = DateTimeOffset.UtcNow };
        // Session delete and other writes: succeed with no body.
        if (method == "DELETE")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("") };

        if (payload == null)
            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = JsonContent(new { error = $"demo: unhandled {method} {ra}" }) };

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(payload) };
    }

    private static HttpContent JsonContent(object obj)
        // WhdApiClient rejects non-JSON bodies, so even an "empty" body is an empty object.
        => new StringContent(obj is string s ? (s.Length == 0 ? "{}" : s) : JsonSerializer.Serialize(obj, WhdJson.Options),
            Encoding.UTF8, "application/json");

    private static int IdOf(string path) => int.Parse(path[(path.LastIndexOf('/') + 1)..]);

    private static string ReadBody(HttpRequestMessage req)
        => req.Content == null ? "" : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    private static int Int(Dictionary<string, string> q, string key)
        => q.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : 0;

    private static Dictionary<string, string> ParseQuery(string query)
        => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .GroupBy(a => Uri.UnescapeDataString(a[0]))
            .ToDictionary(g => g.Key, g => g.First().Length > 1 ? Uri.UnescapeDataString(g.First()[1]) : "");
}

/// <summary>Fabricated people, schools and tickets. All names are fictional.</summary>
internal static class DemoData
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public static readonly List<Tech> Techs = new()
    {
        new() { Id = 1, Type = "Tech", FirstName = "Alex", LastName = "Morgan", Username = "amorgan", Email = "amorgan@example.com" },
        new() { Id = 2, Type = "Tech", FirstName = "Bailey", LastName = "Chen", Username = "bchen", Email = "bchen@example.com" },
        new() { Id = 3, Type = "Tech", FirstName = "Casey", LastName = "Patel", Username = "cpatel", Email = "cpatel@example.com" },
        new() { Id = 4, Type = "Tech", FirstName = "Devon", LastName = "Reyes", Username = "dreyes", Email = "dreyes@example.com" },
        new() { Id = 5, Type = "Tech", FirstName = "Emerson", LastName = "Vale", Username = "evale", Email = "evale@example.com", Inactive = true },
    };

    public static readonly List<Client> Clients = new()
    {
        // The demo tech's client record — lets "default reporter to current user" work in demo mode.
        new() { Id = 100, Type = "Client", FirstName = "Alex", LastName = "Morgan", Username = "amorgan", Email = "amorgan@example.com" },
        new() { Id = 101, Type = "Client", FirstName = "Harper", LastName = "Quinn", Username = "hquinn", Email = "hquinn@example.com" },
        new() { Id = 102, Type = "Client", FirstName = "Finley", LastName = "Brooks", Username = "fbrooks", Email = "fbrooks@example.com" },
        new() { Id = 103, Type = "Client", FirstName = "Rowan", LastName = "Ellis", Username = "rellis", Email = "rellis@example.com" },
        new() { Id = 104, Type = "Client", FirstName = "Sage", LastName = "Turner", Username = "sturner", Email = "sturner@example.com" },
        new() { Id = 105, Type = "Client", FirstName = "Emery", LastName = "Walsh", Username = "ewalsh", Email = "ewalsh@example.com" },
        new() { Id = 106, Type = "Client", FirstName = "Jordan", LastName = "Lee", Username = "jlee", Email = "jlee@example.com" },
        new() { Id = 107, Type = "Client", FirstName = "Taylor", LastName = "Kim", Username = "tkim", Email = "tkim@example.com" },
        new() { Id = 108, Type = "Client", FirstName = "Robin", LastName = "Ortiz", Username = "rortiz", Email = "rortiz@example.com" },
        new() { Id = 109, Type = "Client", FirstName = "Quinn", LastName = "Avery", Username = "qavery", Email = "qavery@example.com" },
        new() { Id = 110, Type = "Client", FirstName = "Reese", LastName = "Nolan", Username = "rnolan", Email = "rnolan@example.com" },
    };

    public static readonly List<StatusType> StatusTypes = new()
    {
        new() { Id = 1, Type = "StatusType", StatusTypeName = "Open" },
        new() { Id = 2, Type = "StatusType", StatusTypeName = "In Progress" },
        new() { Id = 3, Type = "StatusType", StatusTypeName = "Pending" },
        new() { Id = 4, Type = "StatusType", StatusTypeName = "Resolved" },
        new() { Id = 5, Type = "StatusType", StatusTypeName = "Closed" },
        new() { Id = 6, Type = "StatusType", StatusTypeName = "Cancelled" },
        new() { Id = 7, Type = "StatusType", StatusTypeName = "Approval Pending" },
    };

    public static readonly List<PriorityType> PriorityTypes = new()
    {
        new() { Id = 1, Type = "PriorityType", PriorityTypeName = "Low", DisplayOrder = 1 },
        new() { Id = 2, Type = "PriorityType", PriorityTypeName = "Normal", DisplayOrder = 2 },
        new() { Id = 3, Type = "PriorityType", PriorityTypeName = "High", DisplayOrder = 3 },
        new() { Id = 4, Type = "PriorityType", PriorityTypeName = "Urgent", DisplayOrder = 4 },
    };

    public static readonly List<Location> Locations = new()
    {
        new() { Id = 10, Type = "Location", LocationName = "Maplewood Elementary" },
        new() { Id = 11, Type = "Location", LocationName = "Ridgeline Secondary" },
        new() { Id = 12, Type = "Location", LocationName = "Cedar Grove Middle School" },
        new() { Id = 13, Type = "Location", LocationName = "Harbourview Elementary" },
        new() { Id = 14, Type = "Location", LocationName = "District Office" },
    };

    public static readonly List<RequestType> RequestTypes = new()
    {
        RT(100, "Accounts & Access"),
        RT(110, "Email", 100),
        RT(111, "Shared Mailbox Access", 110),
        RT(112, "Distribution List Change", 110),
        RT(120, "Accounts", 100),
        RT(121, "New Account / Access", 120),
        RT(122, "Password Reset", 120, hideSubject: true),
        RT(200, "Hardware"),
        RT(210, "Chromebooks", 200),
        RT(211, "Repair Request", 210),
        RT(220, "Printers", 200),
        RT(221, "Repair / Maintenance", 220),
        RT(230, "AV Equipment", 200),
        RT(231, "Repair / Setup", 230),
        RT(300, "Network"),
        RT(310, "Wi-Fi", 300),
        RT(311, "Coverage Issue", 310),
        RT(400, "Software"),
        RT(410, "Install Request", 400),
        RT(900, "[A] Legacy Phones", archived: true),
        RT(910, "[A] Line Move", 900, archived: true),
    };

    private static RequestType RT(int id, string name, int? parent = null, bool archived = false, bool hideSubject = false)
        => new()
        {
            Id = id,
            Type = "RequestType",
            ProblemTypeName = name,
            DetailDisplayName = name,
            ParentId = parent,
            Archived = archived,
            HideSubject = hideSubject
        };

    private record TicketSeed(
        int Id, string Subject, int Status, int Priority, int Location, int ReqType,
        int Client, int? Tech, double AgeDays, double UpdatedHours, string? Room = null);

    private static readonly TicketSeed[] Seeds =
    {
        new(1001, "Chromebook cart #4 — three devices not charging", 1, 3, 10, 211, 101, 1, 2.0, 3),
        new(1002, "Shared mailbox access request — front office", 1, 2, 14, 111, 102, 1, 1.2, 20),
        new(1003, "Wi-Fi dead spot in the gymnasium", 2, 3, 11, 311, 103, 2, 5.0, 26),
        new(1004, "New staff laptop setup — starts Monday", 3, 2, 12, 121, 104, 1, 3.4, 8),
        new(1005, "Install request: LEGO SPIKE Prime software", 7, 1, 10, 410, 105, 3, 6.1, 50),
        new(1006, "Projector in room 204 flickering", 1, 2, 11, 231, 106, 2, 0.6, 5),
        new(1007, "Student password reset", 4, 1, 13, 122, 107, 1, 4.2, 70),
        new(1008, "Library printer queue stuck — jobs not releasing", 1, 4, 12, 221, 108, 2, 0.3, 1),
        new(1009, "Account provisioning — new support staff hire", 1, 3, 14, 121, 109, 1, 1.8, 12),
        new(1010, "iPad not mirroring to classroom display", 4, 2, 10, 231, 110, 4, 7.5, 90),
        new(1011, "MFA enrolment help", 5, 1, 14, 121, 101, 1, 9.0, 200),
        new(1012, "Interactive board calibration drift", 3, 1, 11, 231, 102, 3, 8.2, 30),
        new(1013, "Laptop battery swelling — safety check needed", 1, 4, 12, 211, 103, 1, 0.4, 2),
        new(1014, "Guest Wi-Fi vouchers for parent night", 4, 1, 14, 311, 104, 2, 11.0, 240),
        new(1015, "Robotics club laptop imaging (x12)", 2, 2, 11, 410, 105, 1, 4.8, 18),
        new(1016, "Voicemail reset — room 112", 1, 1, 13, 910, 106, 4, 2.2, 40),
        new(1017, "Field trip iPad prep — 30 devices", 3, 2, 10, 410, 107, 4, 5.5, 60),
        new(1018, "Email distribution list update — grade 8 team", 1, 2, 14, 112, 108, 1, 1.5, 6),
        new(1019, "Outlook profile rebuild", 4, 2, 11, 111, 109, 2, 6.7, 100),
        new(1020, "Document camera not detected", 1, 3, 12, 231, 110, 1, 0.9, 4),
        new(1021, "VPN access request", 7, 2, 14, 121, 101, 3, 3.1, 28),
        new(1022, "PA system audio from presentation laptop", 2, 3, 13, 231, 102, 2, 2.8, 10),
        new(1023, "Library catalog workstation freezing", 1, 2, 10, 410, 103, 1, 1.1, 7),
        new(1024, "SSD upgrade — front office PC", 3, 1, 14, 221, 104, 4, 10.0, 150),
        new(1025, "Report card system access", 1, 3, 11, 121, 105, 1, 0.5, 2),
        new(1026, "Security camera NVR storage alert", 1, 4, 12, 200, 106, 2, 0.8, 3),
        new(1027, "Staff IT onboarding session", 4, 1, 14, 121, 107, 1, 12.0, 300),
        new(1028, "Chromebook — cracked screen", 1, 2, 13, 211, 108, null, 1.6, 9),
    };

    public static readonly List<Ticket> All = Seeds.Select(s => BuildTicket(s)).ToList();
    public static readonly List<Ticket> Mine = All.Where(t => t.ClientTech?.Id == 1).ToList();

    private static Ticket BuildTicket(TicketSeed s)
    {
        var status = StatusTypes.First(x => x.Id == s.Status);
        var priority = PriorityTypes.First(x => x.Id == s.Priority);
        var location = Locations.First(x => x.Id == s.Location);
        var reqType = RequestTypes.First(x => x.Id == s.ReqType);
        var client = Clients.First(x => Equals(x.Id, s.Client));
        var tech = s.Tech == null ? null : Techs.First(x => x.Id == s.Tech);
        var reported = Now.AddDays(-s.AgeDays);
        var updated = Now.AddHours(-s.UpdatedHours);

        var ticket = new Ticket
        {
            Id = s.Id,
            Type = "Ticket",
            Subject = s.Subject,
            ShortSubject = s.Subject,
            Detail = s.Id == 1001 ? FeaturedDetail
                : s.Id == 1003 ? HtmlDetail
                : $"Reported issue: {s.Subject}.",
            ShortDetail = s.Subject,
            DisplayClient = client.DisplayName,
            ReportDateUtc = reported,
            LastUpdated = updated,
            LastUpdatedUtc = updated,
            PrettyLastUpdated = $"{s.UpdatedHours:0} hours ago",
            StatusTypeId = status.Id,
            StatusType = status,
            PriorityType = priority,
            ProblemType = reqType,
            LocationId = location.Id,
            Location = location,
            Room = s.Room,
            ClientReporter = client,
            ClientTech = tech,
            BookmarkableLink = $"{DemoDataHandler.DemoServerUrl}/helpdesk/WebObjects/Helpdesk.woa/wa/TicketActions/view?ticket={s.Id}",
        };
        if (s.Id == 1001)
        {
            ticket.Room = "Library workroom";
            ticket.Attachments = new List<TicketAttachment>
            {
                new() { Id = 801, Type = "TicketAttachment", FileName = "cart4-inventory.xlsx", UploadDate = reported.AddHours(1), Size = 18432 },
            };
        }
        if (s.Id == 1003)
        {
            // Mimics an email-generated ticket with many inline images — exercises
            // the capped, scrollable ticket-attachments section.
            ticket.Attachments = Enumerable.Range(1, 12)
                .Select(i => new TicketAttachment
                {
                    Id = 820 + i, Type = "TicketAttachment",
                    FileName = $"site-survey-{i:00}.png",
                    UploadDate = reported.AddHours(1), Size = 37_000 * i,
                })
                .ToList();
        }
        return ticket;
    }

    private const string FeaturedDetail =
        "[b]Chromebook Cart #4[/b] — Maplewood Elementary\n\n" +
        "Three devices on the cart are not charging:\n" +
        "[list]\n" +
        "[*]Slot 4 — no charge LED at all\n" +
        "[*]Slot 7 — charges intermittently\n" +
        "[*]Slot 12 — [i]very[/i] loose barrel connector\n" +
        "[/list]\n" +
        "The cart lives in the library workroom. Asset tags are in the attached inventory sheet.\n" +
        "[code]CB-1184, CB-1191, CB-1203[/code]\n" +
        "See the cart layout: [url=https://example.com/maps/maplewood]Maplewood floor plan[/url]";

    // Request details come back from WHD as HTML (unlike BBCode notes) — this seed
    // exercises the HTML rendering path in demo mode.
    private const string HtmlDetail =
        "Direct download: https://example.com/firmware/switches-9.3.2.iso<br />" +
        "Vendor notes: &lt;https://example.com/docs/switch-firmware&gt;<br />" +
        "The lab switches are due for firmware updates before the next maintenance window.<br />" +
        "Planned order of work:<br />" +
        "<ol><li>Back up current configs to the share<br /></li>" +
        "<li>Stage the new firmware on switch <b>A2</b> first<br /></li>" +
        "<li>Verify VLAN tables &amp; uplinks after reboot<br /></li>" +
        "<li>Roll out to the remaining switches</li></ol>" +
        "Exact versions are in the <a href=\"https://example.com/firmware/switches\">firmware matrix</a>.<br />" +
        "Contact: <a href=\"mailto:helpdesk&#64;example.com\">helpdesk&#64;example.com</a>";

    public static List<TicketNote> NotesFor(int ticketId)
    {
        var ticket = All.FirstOrDefault(t => t.Id == ticketId) ?? All[0];
        var client = ticket.ClientReporter!;
        var tech = ticket.ClientTech!;
        if (ticketId != 1001)
        {
            return new List<TicketNote>
            {
                new()
                {
                    Id = ticketId * 10 + 2, Type = "TechNote",
                    NoteText = "Looking into this now — will update shortly.",
                    DateUtc = Now.AddHours(-2), Tech = tech, ClientTech = tech,
                },
                new()
                {
                    Id = ticketId * 10 + 1, Type = "ClientNote",
                    NoteText = "Reported just now. Happy to provide more detail if needed.",
                    DateUtc = Now.AddHours(-5), Client = client,
                },
            };
        }

        return new List<TicketNote>
        {
            new()
            {
                Id = 5004, Type = "ClientNote",
                NoteText = "Any update? Classes start using the cart again on Monday.",
                DateUtc = Now.AddHours(-3), Client = client,
            },
            new()
            {
                Id = 5003, Type = "TechNote", IsHidden = true,
                NoteText = "Replacement barrel connectors ordered (PO #4471). ETA Thursday.",
                DateUtc = Now.AddHours(-6), Tech = tech, ClientTech = tech,
            },
            new()
            {
                Id = 5002, Type = "TechNote", IsSolution = true,
                NoteText = "Inspected the cart this morning. Slot 4's barrel connector is cracked — [b]do not use slot 4[/b] until the part arrives. Slots 7 and 12 need re-soldering. Photo attached.",
                DateUtc = Now.AddHours(-20), Tech = tech, ClientTech = tech,
                Attachments = new List<TicketAttachment>
                {
                    new() { Id = 802, Type = "TicketAttachment", FileName = "slot4-connector.jpg", UploadDate = Now.AddHours(-20), Size = 248110 },
                },
            },
            new()
            {
                Id = 5001, Type = "ClientNote",
                NoteText = "Added the asset tags to the ticket. Cart is locked in the workroom overnight.",
                DateUtc = Now.AddHours(-30), Client = client,
            },
        };
    }

    /// <summary>Mirrors the real server's style=details ticket response: note stubs with attachments embedded.</summary>
    public static Ticket WithEmbeddedNotes(Ticket ticket)
    {
        ticket.EmbeddedNotes = NotesFor(ticket.Id);
        return ticket;
    }

    public static Tech TechById(int id) => Techs.FirstOrDefault(t => t.Id == id) ?? Techs[0];
    public static RequestType RequestTypeById(int id) => RequestTypes.FirstOrDefault(r => r.Id == id) ?? RequestTypes[0];

    /// <summary>Applies an update payload to a ticket (demo: clientTech, statustype, prioritytype, problemtype) and returns it.</summary>
    public static Ticket ApplyTicketUpdate(int id, string body)
    {
        var ticket = TicketById(id);
        if (string.IsNullOrWhiteSpace(body)) return ticket;
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("clientTech", out var tech))
        {
            ticket.ClientTech = tech.ValueKind == JsonValueKind.Null ? null : TechById(IntOf(tech));
            ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
            ticket.LastUpdated = DateTimeOffset.UtcNow;
            ticket.PrettyLastUpdated = "just now";
        }
        if (doc.RootElement.TryGetProperty("problemtype", out var pt) && IntOf(pt) > 0)
        {
            var reqType = RequestTypes.FirstOrDefault(r => r.Id == IntOf(pt));
            if (reqType != null) ticket.ProblemType = reqType;
            ticket.LastUpdatedUtc = DateTimeOffset.UtcNow;
            ticket.LastUpdated = DateTimeOffset.UtcNow;
            ticket.PrettyLastUpdated = "just now";
        }
        return ticket;
    }

    private static int IntOf(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)
            ? n
            : e.ValueKind == JsonValueKind.Object && e.TryGetProperty("id", out var id)
                ? IntOf(id)
                : 0;

    /// <summary>Unknown ids (e.g. opened by number) get a plausible generated ticket.</summary>
    public static Ticket TicketById(int id) => All.FirstOrDefault(t => t.Id == id) ?? BuildTicket(
        new TicketSeed(id, $"Follow-up: scheduled maintenance window #{id}", 1, 2, 14, 121, 105, 1, id % 14 + 0.5, id % 48 + 1));

    /// <summary>Applies the API's 1-based page/limit paging to a pool.</summary>
    public static List<Ticket> Page(List<Ticket> pool, Dictionary<string, string> q)
    {
        var page = q.TryGetValue("page", out var p) && int.TryParse(p, out var pn) ? pn : 1;
        var limit = q.TryGetValue("limit", out var l) && int.TryParse(l, out var ln) ? ln : 100;
        return pool.Skip((page - 1) * limit).Take(limit).ToList();
    }

    /// <summary>Honors the simple id-equality qualifiers the demo saved filters use; anything else returns the full pool.</summary>
    public static List<Ticket> Search(string? qualifier)
    {
        if (string.IsNullOrEmpty(qualifier)) return All;
        if (Regex.IsMatch(qualifier, @"clientTech\s*=\s*null"))
            return All.Where(t => t.ClientTech == null).ToList();
        var m = Regex.Match(qualifier, @"problemType\.id\s*=\s*(\d+)");
        if (m.Success) return All.Where(t => t.ProblemType?.Id == int.Parse(m.Groups[1].Value)).ToList();
        m = Regex.Match(qualifier, @"prioritytype\.id\s*=\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success) return All.Where(t => t.PriorityType?.Id == int.Parse(m.Groups[1].Value)).ToList();
        return All;
    }
}
