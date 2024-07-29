using System.IO.Compression;
using System.Security.Cryptography;
using LiteDB;

using var db = new LiteDatabase("database.db");
var fileStorage = db.FileStorage;

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
}

void CopyFile(string rawFileName)
{
    var fileName = Path.GetFileName(rawFileName);
    var collection = db.GetCollection<ZipData>("zipdata");

    collection.EnsureIndex(x => x.FileName);
    if (collection.Exists(x => x.FileName == fileName))
    {
        Console.WriteLine($"File {fileName} already saved in DB");
        return;
    }

    var zipdata = new ZipData
    {
        FileName = fileName,
        Files = [SaveFileInDb(rawFileName)]
    };

    collection.Insert(zipdata);
}


void Unzip(string path)
{
    var collection = db.GetCollection<ZipData>("zipdata");
    var fileName = Path.GetFileName(path);
    collection.EnsureIndex(x => x.FileName);

    if (collection.Exists(x => x.FileName == fileName))
    {
        Console.WriteLine($"Zip {fileName} already saved in DB");
        return;
    }

    var zipdata = new ZipData { FileName = fileName };

    using var archive = ZipFile.OpenRead(path);
    foreach (var entry in archive.Entries)
    {
        var hash = SaveFileInDb(entry.FullName, entry);
        zipdata.Files.Add(hash);
    }

    collection.Insert(zipdata);
}

string SaveFileInDb(string filename, ZipArchiveEntry? data = null)
{
    using var md5 = MD5.Create();
    using var stream = data != null ? data.Open() : File.OpenRead(filename);

    var hash = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();

    if (fileStorage.FindById(hash) == null)
    {
        using var storeStream = data != null ? data.Open() : File.OpenRead(filename);
        fileStorage.Upload(hash, filename, storeStream);
        Console.WriteLine($"File {filename} store {hash}");
    }
    else
    {
        Console.WriteLine($"File {filename} {hash} already in DB");
    }

    return hash;
}

public record ZipData
{
    public required string FileName { get; set; }
    public List<string> Files { get; set; } = [];
}
