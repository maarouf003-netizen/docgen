namespace DocGenerator.Application.Common.Options;

public class WordTemplatesOptions
{
    public string Path { get; set; } = "WordTemplates";

    public Dictionary<string, string> Templates { get; set; } = new()
    {
        ["001"] = "summon.docx",
        ["002"] = "record.docx",
        ["003"] = "notice.docx",
        ["004"] = "Seizure.docx",
        ["005"] = "property.docx",
    };
}
