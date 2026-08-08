namespace DocGenerator.Domain.Entities;

public class RealEstate
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string? Property { get; set; }
    public string? PropertyNumber { get; set; }
    public string? PropertyDistrict { get; set; }
    public string? LandRegistry { get; set; }
    public string? ShareType { get; set; }

    public ICollection<RealEstateOwner> Owners { get; set; } = new List<RealEstateOwner>();

    public Document Document { get; set; } = null!;
}
