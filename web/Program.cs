using System.IO.Compression;
using System.Net;
using LiteDB;

var db = new LiteDatabase("database.db");

#if DEBUG
var baseUrl = "http://127.0.0.1:8080";
#else
var baseUrl = "";
#endif

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// wget http://localhost:5105/getfile/0d6e54b419381_tower_crane_v7.zip -O test.zip
app.MapGet("/getfile/{file}", async (string file) =>
    {
        var collection = db.GetCollection<ZipData>("zipdata");
        collection.EnsureIndex(x => x.FileName);
        var fileData = collection.FindOne(x => x.FileName == file);
        if (fileData == null)
            return Results.NotFound();

        var fileType = Path.GetExtension(file);
        var dlClient = new HttpClient();

        try
        {
            switch (fileType)
            {
                case ".zip":
                case ".skinzip":
                    app.Logger.LogInformation($"Rebuilding zipfile {file}");
                    var memoryStream = new MemoryStream();
                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        foreach (var data in fileData.Files)
                        {
                            var fp = $"{data.Hash[0]}/{data.Hash[1]}/{data.Hash[2]}/{data.Hash}{Path.GetExtension(data.Name)}";
                            var archiveFile = archive.CreateEntry(data.Name, CompressionLevel.Fastest);
                            await using var entryStream = archiveFile.Open();
                            var zipfileStream = await dlClient.GetStreamAsync($"{baseUrl}/{fp}");
                            zipfileStream.CopyTo(entryStream);
                        }
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    return Results.File(memoryStream, "application/zip", file);
                default:
                    app.Logger.LogInformation($"Proxying {file}");
                    var rf = fileData.Files.First();
                    var storedFilePath = $"{rf.Hash[0]}/{rf.Hash[1]}/{rf.Hash[2]}/{rf.Hash}{Path.GetExtension(file)}";
                    var stream = await dlClient.GetStreamAsync($"{baseUrl}/{storedFilePath}");
                    return Results.File(stream, fileDownloadName: rf.Name);
            }
        }
        catch (HttpRequestException e)
        {
            app.Logger.LogError(e.Message);
            return e.StatusCode == HttpStatusCode.NotFound ? Results.NotFound() : Results.BadRequest();
        }
    })
    .WithOpenApi();

app.Run();

public record ZipData
{
    public required string FileName { get; set; }
    public List<HashedFile> Files { get; set; } = [];
}

public record HashedFile
{
    public required string Name { get; set; }
    public required string Hash { get; set; }
}