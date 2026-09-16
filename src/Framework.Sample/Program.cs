var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
    <head><meta charset="utf-8"><title>Razor-Wings</title></head>
    <body>
      <h1>Razor-Wings</h1>
      <p><a href="/counter">Open the Counter demo</a></p>
    </body>
    </html>
    """,
    "text/html"));

app.MapGet("/counter", () =>
{
    var componentHtml = RazorWings.Components.CounterRenderer.Render(0);
    var page = $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Counter</title></head>
        <body>
          {{componentHtml}}
          <script type="module">
            import { mountCounter } from '/generated/Counter.g.js';
            mountCounter(document.querySelector('.counter'));
          </script>
        </body>
        </html>
        """;

    return Results.Content(page, "text/html");
});

app.Run();
