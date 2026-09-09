namespace WindowsDevApp.Models;

public class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New profile";
    public string RepoPath { get; set; } = "";
    public string EnvFilePath { get; set; } = "";
    public string ServiceName { get; set; } = "";
}
