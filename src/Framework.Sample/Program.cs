var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Razor-Wings PoC - Component generation framework");

app.Run();
