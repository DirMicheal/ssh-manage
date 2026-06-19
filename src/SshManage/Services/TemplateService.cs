using SshManage.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SshManage.Services;

public class TemplateService
{
    private readonly string _templateDirectory;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public TemplateService()
    {
        var sshDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        _templateDirectory = Path.Combine(sshDir, "ssh-manage", "templates");
    }

    public List<SiteTemplate> GetAllTemplates()
    {
        var templates = new List<SiteTemplate>();

        if (!Directory.Exists(_templateDirectory))
            return templates;

        foreach (var file in Directory.GetFiles(_templateDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file, new UTF8Encoding(false));
                var template = JsonSerializer.Deserialize<SiteTemplate>(json);
                if (template != null)
                    templates.Add(template);
            }
            catch { }
        }

        return templates.OrderBy(t => t.Name).ToList();
    }

    public bool SaveTemplate(SiteTemplate template)
    {
        try
        {
            Directory.CreateDirectory(_templateDirectory);

            var fileName = GetSafeFileName(template.Name) + ".json";
            var filePath = Path.Combine(_templateDirectory, fileName);

            template.CreatedAt = DateTime.Now;
            var json = JsonSerializer.Serialize(template, _jsonOptions);
            File.WriteAllText(filePath, json, new UTF8Encoding(false));

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool DeleteTemplate(string templateName)
    {
        try
        {
            var fileName = GetSafeFileName(templateName) + ".json";
            var filePath = Path.Combine(_templateDirectory, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool HasDuplicateName(string name, string? excludeName = null)
    {
        var templates = GetAllTemplates();
        return templates.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && !t.Name.Equals(excludeName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "template" : safe;
    }
}
