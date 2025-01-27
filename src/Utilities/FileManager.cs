using System.IO;

public static class FileManager
{
    public static void Save(string filePath, string content)
    {
        File.WriteAllText(filePath, content);
    }

    public static string Load(string filePath)
    {
        return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
    }
}
