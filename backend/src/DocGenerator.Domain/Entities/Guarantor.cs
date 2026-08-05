namespace DocGenerator.Domain.Entities;

public class Guarantor
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int GuarantorNumber { get; set; }
    public string? GuarantorName { get; set; }
    public string? GuarantorFather { get; set; }
    public string? GuarantorFamily { get; set; }
    public string? GuarantorMother { get; set; }
    public string? GuarantorBirth { get; set; }
    public string? GuarantorRegister { get; set; }
    public string? GuarantorNationalId { get; set; }
    public string? GuarantorAddress { get; set; }
    public string? AddressType { get; set; } = "موطن مختار";

    public Document Document { get; set; } = null!;
}
