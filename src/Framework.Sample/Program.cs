// Uncomment to run validation before starting web server
// var args = Environment.GetCommandLineArgs();
// if (args.Contains("--validate"))
// {
//     PipelineValidator.ValidateCounterComponent();
//     return;
// }

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Razor-Wings PoC - Component generation framework");

app.Run();
