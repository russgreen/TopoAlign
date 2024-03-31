using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace _build;

class ApplicationPackage
{
    public string SchemaVersion { get; set; }
    public string AutodeskProduct { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string AppVersion { get; set; }
    public string FriendlyVersion { get; set; }
    public string ProductType { get; set; }
    public string HelpFile { get; set; }
    public string SupportedLocales { get; set; }
    public string AppNameSpace { get; set; }
    public string OnlineDocumentation { get; set; }
    public string Author { get; set; }
    public string ProductCode { get; set; }
    public string UpgradeCode { get; set; }
    public string Icon { get; set; }
    public CompanyDetails CompanyDetails { get; set; }
    public RuntimeRequirements RuntimeRequirements { get; set; }
    public List<Components> Components { get; set; }

    public ApplicationPackage()
    {
        Components = new List<Components>();
    }

    public void LoadFromXml(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var root = doc.Element("ApplicationPackage");
        SchemaVersion = root.Attribute("SchemaVersion").Value;
        AutodeskProduct = root.Attribute("AutodeskProduct").Value;
        Name = root.Attribute("Name").Value;
        Description = root.Attribute("Description").Value;
        AppVersion = root.Attribute("AppVersion").Value;
        FriendlyVersion = root.Attribute("FriendlyVersion").Value;
        ProductType = root.Attribute("ProductType").Value;
        HelpFile = root.Attribute("HelpFile").Value;
        SupportedLocales = root.Attribute("SupportedLocales").Value;
        AppNameSpace = root.Attribute("AppNameSpace").Value;
        OnlineDocumentation = root.Attribute("OnlineDocumentation").Value;
        Author = root.Attribute("Author").Value;
        ProductCode = root.Attribute("ProductCode").Value;
        UpgradeCode = root.Attribute("UpgradeCode").Value;
        Icon = root.Attribute("Icon").Value;

        var companyDetailsElement = root.Element("CompanyDetails");
        CompanyDetails = new CompanyDetails
        {
            Name = companyDetailsElement.Attribute("Name").Value,
            Url = companyDetailsElement.Attribute("Url").Value,
            Email = companyDetailsElement.Attribute("Email").Value
        };

        var runtimeRequirementsElement = root.Element("RuntimeRequirements");
        RuntimeRequirements = new RuntimeRequirements
        {
            OS = runtimeRequirementsElement.Attribute("OS").Value,
            Platform = runtimeRequirementsElement.Attribute("Platform").Value,
            SeriesMin = runtimeRequirementsElement.Attribute("SeriesMin").Value,
            SeriesMax = runtimeRequirementsElement.Attribute("SeriesMax").Value
        };

        foreach (var componentsElement in root.Elements("Components"))
        {
            var components = new Components
            {
                Description = componentsElement.Attribute("Description").Value,
                RuntimeRequirements = new RuntimeRequirements
                {
                    OS = componentsElement.Element("RuntimeRequirements").Attribute("OS").Value,
                    Platform = componentsElement.Element("RuntimeRequirements").Attribute("Platform").Value,
                    SeriesMin = componentsElement.Element("RuntimeRequirements").Attribute("SeriesMin").Value,
                    SeriesMax = componentsElement.Element("RuntimeRequirements").Attribute("SeriesMax").Value
                },
                ComponentEntry = new ComponentEntry
                {
                    AppName = componentsElement.Element("ComponentEntry").Attribute("AppName").Value,
                    Version = componentsElement.Element("ComponentEntry").Attribute("Version").Value,
                    ModuleName = componentsElement.Element("ComponentEntry").Attribute("ModuleName").Value,
                    AppDescription = componentsElement.Element("ComponentEntry").Attribute("AppDescription").Value
                }
            };
            Components.Add(components);
        }
    }

    public void SaveToXml(string filePath)
    {
        var doc = new XDocument();
        var root = new XElement("ApplicationPackage");
        root.SetAttributeValue("SchemaVersion", SchemaVersion);
        root.SetAttributeValue("AutodeskProduct", AutodeskProduct);
        root.SetAttributeValue("Name", Name);
        root.SetAttributeValue("Description", Description);
        root.SetAttributeValue("AppVersion", AppVersion);
        root.SetAttributeValue("FriendlyVersion", FriendlyVersion);
        root.SetAttributeValue("ProductType", ProductType);
        root.SetAttributeValue("HelpFile", HelpFile);
        root.SetAttributeValue("SupportedLocales", SupportedLocales);
        root.SetAttributeValue("AppNameSpace", AppNameSpace);
        root.SetAttributeValue("OnlineDocumentation", OnlineDocumentation);
        root.SetAttributeValue("Author", Author);
        root.SetAttributeValue("ProductCode", ProductCode);
        root.SetAttributeValue("UpgradeCode", UpgradeCode);
        root.SetAttributeValue("Icon", Icon);

        var companyDetailsElement = new XElement("CompanyDetails");
        companyDetailsElement.SetAttributeValue("Name", CompanyDetails.Name);
        companyDetailsElement.SetAttributeValue("Url", CompanyDetails.Url);
        companyDetailsElement.SetAttributeValue("Email", CompanyDetails.Email);
        root.Add(companyDetailsElement);

        var runtimeRequirementsElement = new XElement("RuntimeRequirements");
        runtimeRequirementsElement.SetAttributeValue("OS", RuntimeRequirements.OS);
        runtimeRequirementsElement.SetAttributeValue("Platform", RuntimeRequirements.Platform);
        runtimeRequirementsElement.SetAttributeValue("SeriesMin", RuntimeRequirements.SeriesMin);
        runtimeRequirementsElement.SetAttributeValue("SeriesMax", RuntimeRequirements.SeriesMax);
        root.Add(runtimeRequirementsElement);

        foreach (var components in Components)
        {
            var componentsElement = new XElement("Components");
            componentsElement.SetAttributeValue("Description", components.Description);

            var componentsRuntimeRequirementsElement = new XElement("RuntimeRequirements");
            componentsRuntimeRequirementsElement.SetAttributeValue("OS", components.RuntimeRequirements.OS);
            componentsRuntimeRequirementsElement.SetAttributeValue("Platform", components.RuntimeRequirements.Platform);
            componentsRuntimeRequirementsElement.SetAttributeValue("SeriesMin", components.RuntimeRequirements.SeriesMin);
            componentsRuntimeRequirementsElement.SetAttributeValue("SeriesMax", components.RuntimeRequirements.SeriesMax);
            componentsElement.Add(componentsRuntimeRequirementsElement);

            var componentEntryElement = new XElement("ComponentEntry");
            componentEntryElement.SetAttributeValue("AppName", components.ComponentEntry.AppName);
            componentEntryElement.SetAttributeValue("Version", components.ComponentEntry.Version);
            componentEntryElement.SetAttributeValue("ModuleName", components.ComponentEntry.ModuleName);
            componentEntryElement.SetAttributeValue("AppDescription", components.ComponentEntry.AppDescription);
            componentsElement.Add(componentEntryElement);

            root.Add(componentsElement);
        }

        doc.Add(root);
        doc.Save(filePath);
    }
}

public class CompanyDetails
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string Email { get; set; }
}

public class RuntimeRequirements
{
    public string OS { get; set; }
    public string Platform { get; set; }
    public string SeriesMin { get; set; }
    public string SeriesMax { get; set; }
}

public class Components
{
    public string Description { get; set; }
    public RuntimeRequirements RuntimeRequirements { get; set; }
    public ComponentEntry ComponentEntry { get; set; }
}

public class ComponentEntry
{
    public string AppName { get; set; }
    public string Version { get; set; }
    public string ModuleName { get; set; }
    public string AppDescription { get; set; }
}
