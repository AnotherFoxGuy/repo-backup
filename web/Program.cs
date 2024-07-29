using System.IO.Compression;
using LiteDB;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var db = new LiteDatabase("database.db");
var fileStorage = db.FileStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

        if (fileType == ".zip" || fileType == ".skinzip")
        {
            var memoryStream = new MemoryStream();
            var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);

            foreach (var fileHash in fileData.Files)
            {
                var f = fileStorage.FindById(fileHash.Hash);
                if (f == null)
                    return Results.NotFound();

                var archiveFile = archive.CreateEntry(fileHash.Name, CompressionLevel.Fastest);
                using var entryStream = archiveFile.Open();
                f.CopyTo(entryStream);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            return Results.Stream(memoryStream, "application/x-zip", file);
        }
        else
        {
            var rf = fileData.Files.First();
            var f = fileStorage.FindById(rf.Hash);
            if (f == null)
                return Results.NotFound();
            return Results.Stream(f.OpenRead(), f.MimeType, rf.Name);
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