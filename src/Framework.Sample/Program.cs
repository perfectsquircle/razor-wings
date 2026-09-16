using System.Collections.Generic;

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
      <ul>
        <li><a href="/counter">Counter</a></li>
        <li><a href="/toggle">Toggle / conditional rendering</a></li>
        <li><a href="/todo-list">Todo list / foreach rendering</a></li>
        <li><a href="/child">Child / component props</a></li>
        <li><a href="/parent">Parent / callback propagation</a></li>
      </ul>
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

app.MapGet("/toggle", () =>
{
    var componentHtml = RazorWings.Components.ToggleRenderer.Render(false);
    var page = $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Toggle</title></head>
        <body>
          <p><a href="/">Back to demos</a></p>
          <main id="toggle-demo">
            {{componentHtml}}
          </main>
          <script type="module">
            import { mountToggle } from '/generated/Toggle.g.js';
            mountToggle(document.querySelector('#toggle-demo'));
          </script>
        </body>
        </html>
        """;

    return Results.Content(page, "text/html");
});

app.MapGet("/todo-list", () =>
{
    var componentHtml = RazorWings.Components.TodoListRenderer.Render(
        new List<string> { "Buy milk", "Walk dog" },
        "");
    var page = $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Todo list</title></head>
        <body>
          <p><a href="/">Back to demos</a></p>
          <main id="todo-list-demo">
            {{componentHtml}}
          </main>
          <script type="module">
            import { mountTodoList } from '/generated/TodoList.g.js';
            mountTodoList(document.querySelector('#todo-list-demo'));
          </script>
        </body>
        </html>
        """;

    return Results.Content(page, "text/html");
});

app.MapGet("/child", () =>
{
    var componentHtml = RazorWings.Components.ChildRenderer.Render(3);
    var page = $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Child component</title></head>
        <body>
          <p><a href="/">Back to demos</a></p>
          <main id="child-demo">
            {{componentHtml}}
          </main>
          <script type="module">
            import { mountChild } from '/generated/Child.g.js';
            mountChild(document.querySelector('#child-demo'), {
              count: 3,
              onIncrement: () => console.log('Child callback invoked')
            });
          </script>
        </body>
        </html>
        """;

    return Results.Content(page, "text/html");
});

app.MapGet("/parent", () =>
{
    var componentHtml = RazorWings.Components.ParentRenderer.Render(0);
    var page = $$"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Parent component</title></head>
        <body>
          <p><a href="/">Back to demos</a></p>
          <main id="parent-demo">
            {{componentHtml}}
          </main>
          <script type="module">
            import { mountParent } from '/generated/Parent.g.js';
            mountParent(document.querySelector('#parent-demo'));
          </script>
        </body>
        </html>
        """;

    return Results.Content(page, "text/html");
});

app.Run();
