using System.Text.Json;
using HelloCompanion.App.Models;

namespace HelloCompanion.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsDirectory;
    private readonly string _settingsPath;

    public SettingsService()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelloCompanion");
        _settingsPath = Path.Combine(_settingsDirectory, "settings.json");
    }

    public async Task<CompanionSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new CompanionSettings();
            }

            await using FileStream stream = File.OpenRead(_settingsPath);
            CompanionSettings? settings =
                await JsonSerializer.DeserializeAsync<CompanionSettings>(stream, SerializerOptions);
            return (settings ?? new CompanionSettings()).Normalize();
        }
        catch (JsonException)
        {
            return new CompanionSettings();
        }
        catch (IOException)
        {
            return new CompanionSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new CompanionSettings();
        }
    }

    public async Task SaveAsync(CompanionSettings settings)
    {
        Directory.CreateDirectory(_settingsDirectory);
        string temporaryPath = _settingsPath + ".tmp";

        await using (FileStream stream = new(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings.Normalize(), SerializerOptions);
            await stream.FlushAsync();
        }

        File.Move(temporaryPath, _settingsPath, true);
    }
}
