namespace ECTSystem.Shared.ViewModels;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class FormSectionAttribute : Attribute
{
    public string SectionName { get; }

    public FormSectionAttribute(string sectionName)
    {
        SectionName = sectionName;
    }
}
