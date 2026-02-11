namespace GithubGet.Core.Models;

public readonly record struct RepositoryId(string Owner, string Repo)
{
    public static RepositoryId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Repository is required.", nameof(value));
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException("Repository must be in owner/repo format.", nameof(value));
        }

        return new RepositoryId(parts[0], parts[1]);
    }

    public override string ToString() => $"{Owner}/{Repo}";
}
