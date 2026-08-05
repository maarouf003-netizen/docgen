namespace DocGenerator.Domain.Entities;

public class DocumentRegistrationDate
{
    public int DocumentId { get; set; }
    public string? Date { get; set; }

    public Document Document { get; set; } = null!;
}
