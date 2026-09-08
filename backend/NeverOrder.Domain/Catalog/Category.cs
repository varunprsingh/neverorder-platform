namespace NeverOrder.Domain.Catalog;

public sealed class Category
{
    private Category()
    {
    }

    public Category(string name, string slug)
    {
        Id = Guid.NewGuid();
        Name = name;
        Slug = slug;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public bool IsActive { get; private set; }

    public void Rename(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
