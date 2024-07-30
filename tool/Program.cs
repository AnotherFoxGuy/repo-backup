using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

List<ZipData> database = [];
var databasePath = "database.json";

if (File.Exists(databasePath))
{
    var jsonString = File.ReadAllText(databasePath);
    database = JsonSerializer.Deserialize<List<ZipData>>(jsonString);
}

var files = Directory.GetFiles("repofiles");
foreach (var rawFileName in files)
{
    var fileType = Path.GetExtension(rawFileName);

    Console.WriteLine($"Processing {rawFileName}");

    try
    {
        switch (fileType)
        {
            case ".zip":
            case ".skinzip":
                Unzip(rawFileName);
                break;
            default:
                CopyFile(rawFileName);
                break;
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }

    var jsonString = JsonSerializer.Serialize(database);
    File.WriteAllText(databasePath, jsonString);
}

void CopyFile(string rawFileName)
{
    var fileName = Path.GetFileName(rawFileName);
    if (database.Exists(x => x.FileName == fileName))
    {
        Console.WriteLine($"File {fileName} has already been stored");
        return;
    }

    var zipdata = new ZipData
    {
        FileName = fileName,
        Files =
        [
            new HashedFile
            {
                Name = fileName,
                Hash = SaveFileInDb(rawFileName)
            }
        ]
    };

    database.Add(zipdata);
}


void Unzip(string path)
{
    var fileName = Path.GetFileName(path);

    if (database.Exists(x => x.FileName == fileName))
    {
        Console.WriteLine($"Zip {fileName} has already been stored");
        return;
    }

    var zipdata = new ZipData { FileName = fileName };

    using var archive = ZipFile.OpenRead(path);
    foreach (var entry in archive.Entries)
    {
        var hash = SaveFileInDb(entry.FullName, entry);
        zipdata.Files.Add(new HashedFile { Name = entry.FullName, Hash = hash });
    }

    database.Add(zipdata);
}

string SaveFileInDb(string filename, ZipArchiveEntry? data = null)
{
    using var hashgen = SHA256.Create();
    using var stream = data != null ? data.Open() : File.OpenRead(filename);

    var hash = BitConverter.ToString(hashgen.ComputeHash(stream)).Replace("-", "").ToLower();
    var storedFilePath = $"./filestorage/{hash[0]}/{hash[1]}/{hash[2]}/{hash}{Path.GetExtension(filename)}";

    if (!File.Exists(storedFilePath))
    {
        var path = Path.GetDirectoryName(storedFilePath);
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        if (data != null)
            data.ExtractToFile(storedFilePath);
        else
            File.Copy(filename, storedFilePath);

        Console.WriteLine($"File {filename} store {hash} stored at {storedFilePath}");
    }
    else
    {
        Console.WriteLine($"File {filename} {hash} has already been stored");
    }

    return hash;
}

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
