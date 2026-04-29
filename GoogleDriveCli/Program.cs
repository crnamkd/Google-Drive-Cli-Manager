using GoogleDriveCli.Models;
using GoogleDriveCli.Services;
using System.CommandLine;
using Spectre.Console;
using System.Diagnostics;

// -------------------------------------------------------------------
// Root setup
// -------------------------------------------------------------------
var rootCommand = new RootCommand("Google Drive CLI Manager");

// -------------------------------------------------------------------
// SYNC COMMAND
// -------------------------------------------------------------------
var syncCommand = new Command("sync", "Downloads all files from Google Drive");
syncCommand.SetHandler(async () =>
{
    var driveService = AuthService.GetDriveService();
    var wrapper = new DriveServiceWrapper(driveService);

        var downloadDir = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
        Directory.CreateDirectory(downloadDir);

        AnsiConsole.MarkupLine("[yellow]Fetching file list...[/]");
        var files = await wrapper.GetAllFilesAsync();

        // Filter only actual files (not folders / Google Docs without binary)
        // In Drive, Google Docs have mimeType 'application/vnd.google-apps.document', etc.
        // We'll skip folders but include binary files.
        var downloadableFiles = files
            .Where(f => f.MimeType != "application/vnd.google-apps.folder")
            .ToList();

        var stats = new SyncStatistics();
        var sw = Stopwatch.StartNew();

        // Parallel download with MaxDegreeOfParallelism = 5
        var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };

        await AnsiConsole.Progress()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Downloading...[/]", maxValue: downloadableFiles.Count);

                await Parallel.ForEachAsync(downloadableFiles, options, async (file, token) =>
                {
                    var localPath = Path.Combine(downloadDir, file.Name);
                    bool exists = File.Exists(localPath);

                    if (!exists)
                    {
                        await wrapper.DownloadFileAsync(file.Id, localPath, stats);
                    }
                    else
                    {
                        stats.IncrementSkipped();
                    }

                    task.Increment(1);
                });
            });

        sw.Stop();

        // Print summary
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Total files", downloadableFiles.Count.ToString());
        table.AddRow("Downloaded", stats.Downloaded.ToString());
        table.AddRow("Skipped (already exist)", stats.Skipped.ToString());
        table.AddRow("Failed", stats.Failed.ToString());
        table.AddRow("Time taken", $"{sw.Elapsed:mm\\:ss\\.fff}");

        AnsiConsole.Write(table);
});

// -------------------------------------------------------------------
// SEARCH COMMAND
// -------------------------------------------------------------------
var searchCommand = new Command("search", "Search files");
var searchQueryArg = new Argument<string>("query", "Search term");
searchCommand.AddArgument(searchQueryArg);
searchCommand.SetHandler(async (string query) =>
{
    var driveService = AuthService.GetDriveService();
    var wrapper = new DriveServiceWrapper(driveService);

    var results = await wrapper.SearchFilesAsync(query);

    var downloadDir = Path.Combine(Directory.GetCurrentDirectory(), "Downloads");
    var localFiles = Directory.Exists(downloadDir)
        ? Directory.GetFiles(downloadDir).Select(Path.GetFileName).ToHashSet()!
        : new HashSet<string>();

    var table = new Table();
    table.AddColumn("Name");
    table.AddColumn("ID");
    table.AddColumn("Status");

    foreach (var file in results)
    {
        string status = localFiles.Contains(file.Name) ? "[green]Downloaded[/]" : "[red]Not Downloaded[/]";
        table.AddRow(file.Name, file.Id, status);
    }

    AnsiConsole.Write(table);
}, searchQueryArg);

// -------------------------------------------------------------------
// UPLOAD COMMAND
// -------------------------------------------------------------------
var uploadCommand = new Command("upload", "Upload a local file");
var localPathArg = new Argument<string>("local-path", "Local file path");
var drivePathArg = new Argument<string>("drive-path", "Target folder in Drive (e.g., 'Folder/SubFolder')");
uploadCommand.AddArgument(localPathArg);
uploadCommand.AddArgument(drivePathArg);
uploadCommand.SetHandler(async (string localPath, string drivePath) =>
{
    if (!File.Exists(localPath))
    {
        AnsiConsole.MarkupLine("[red]Local file not found.[/]");
        return;
    }

    var driveService = AuthService.GetDriveService();
    var wrapper = new DriveServiceWrapper(driveService);

    // Resolve / create folder
    string? folderId = await wrapper.ResolveFolderIdByPathAsync(drivePath);

    if (folderId == null)
    {
        AnsiConsole.MarkupLine("[red]Target folder path does not exist in Drive.[/]");
        return;
    }

    var uploaded = await wrapper.UploadFileAsync(localPath, folderId);
    AnsiConsole.MarkupLine($"[green]Uploaded '{uploaded?.Name}' (ID: {uploaded?.Id})[/]");
}, localPathArg, drivePathArg);

// -------------------------------------------------------------------
// Add commands to root
// -------------------------------------------------------------------
rootCommand.AddCommand(syncCommand);
rootCommand.AddCommand(searchCommand);
rootCommand.AddCommand(uploadCommand);

// -------------------------------------------------------------------
// Main loop - runs until user types 'exit'
// -------------------------------------------------------------------
AnsiConsole.MarkupLine("[bold cyan]Google Drive CLI Manager[/]");
AnsiConsole.MarkupLine("[dim]Type 'exit' to quit the application[/]");
AnsiConsole.WriteLine();

void ShowAvailableCommands()
{
    var panel = new Panel(
        new Markup(
            "[bold yellow]Available Commands:[/]\n\n" +
            "[cyan]sync[/]                          - Downloads all files from Google Drive\n" +
            "[cyan]search <query>[/]              - Search files by name or content\n" +
            "[cyan]upload <local-path> <drive-path>[/] - Upload a local file to Google Drive\n" +
            "[cyan]exit[/]                          - Exit the application"
        ))
    {
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(foreground: Color.Green)
    };

    AnsiConsole.Write(panel);
    AnsiConsole.WriteLine();
}

while (true)
{
    ShowAvailableCommands();

    var input = AnsiConsole.Ask<string>("[bold green]>[/] Enter command:");

    if (input.Trim().ToLower() == "exit")
    {
        AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
        break;
    }

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    // Parse and execute the command
    var commandArgs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    await rootCommand.InvokeAsync(commandArgs);

    AnsiConsole.WriteLine();
}

return 0;