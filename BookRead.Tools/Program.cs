using System.Reflection;

var assembly = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "BookRead.dll"));
var sourceType = assembly.GetType("BookRead.Models.OpdsSource")!;
var clientType = assembly.GetType("BookRead.Services.OpdsClient")!;
var pageType = assembly.GetType("BookRead.Models.OpdsPage")!;
var source = Activator.CreateInstance(sourceType)!;
sourceType.GetProperty("Name")!.SetValue(source, "gutenberg");
sourceType.GetProperty("Url")!.SetValue(source, "https://www.gutenberg.org/ebooks.opds/");
using var client = new HttpClient();
var opds = Activator.CreateInstance(clientType, client)!;
var loadMethod = clientType.GetMethod("LoadPageAsync")!;
var task = (Task)loadMethod.Invoke(opds, new[] { source, (string?)null, CancellationToken.None })!;
await task;
object pageValue = task.GetType().GetProperty("Result")!.GetValue(task)!;
string title = (string)pageType.GetProperty("Title")!.GetValue(pageValue)!;
var entries = (System.Collections.IEnumerable)pageType.GetProperty("Entries")!.GetValue(pageValue)!;
string? search = (string?)pageType.GetProperty("SearchTemplateUrl")!.GetValue(pageValue);
Console.WriteLine($"Title={title}; Entries={entries.Cast<object>().Count()}; Search={search}");
