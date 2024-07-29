using System.IO.Compression;
using LiteDB;

var db = new LiteDatabase("database.db");
var fileStorage = db.FileStorage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/getfile/{file}", (string file) =>
    {
        var collection = db.GetCollection<ZipData>("zipdata");
        collection.EnsureIndex(x => x.FileName);
        var fileData = collection.FindOne(x => x.FileName == file);
        if (fileData == null)
            return Results.NotFound();

        var fileType = Path.GetExtension(file);

        switch (fileType)
        {
            case ".zip":
            case ".skinzip":
                var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var fileHash in fileData.Files)
                    {
                        var cf = fileStorage.FindById(fileHash);
                        if (cf == null)
                            return Results.NotFound();
                        var archiveFile = archive.CreateEntry(cf.Filename, CompressionLevel.Fastest);
                        using var entryStream = archiveFile.Open();
                        cf.CopyTo(entryStream);
                    }
                }

                memoryStream.Seek(0, SeekOrigin.Begin);
                return Results.File(memoryStream, "application/zip", file);
            default:
                var rf = fileData.Files.First();
                var f = fileStorage.FindById(rf);
                if (f == null)
                    return Results.NotFound();
                return Results.File(f.OpenRead(), f.MimeType, f.Filename);
        }
    })
    .WithOpenApi();

app.Run();

public record ZipData
{
    public required string FileName { get; set; }
    public List<string> Files { get; set; } = [];
}