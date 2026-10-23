using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;

namespace SandboxStudio.UI.ViewModels;

/// <summary>
/// ViewModel controlling the creation and detailed editing of a Sandbox profile.
/// </summary>
public sealed partial class EditProfileViewModel : ObservableObject
{
    private readonly IProfileRepository _repository;
    private readonly IWsbValidator _validator;

    /// <summary>
    /// Gets or sets the draft profile being edited.
    /// </summary>
    [ObservableProperty]
    public partial SandboxProfile Profile { get; set; } = new();

    /// <summary>
    /// Gets or sets the editable collection of mapped folders.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<WsbMappedFolder> MappedFolders { get; set; } = new();

    /// <summary>
    /// Gets or sets the aggregated validation summary displayed to the user.
    /// </summary>
    [ObservableProperty]
    public partial string? ValidationSummary { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current draft has validation errors.
    /// </summary>
    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="EditProfileViewModel"/>.
    /// </summary>
    public EditProfileViewModel(
        IProfileRepository repository,
        IWsbValidator validator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Sets the active profile for editing and populates observable fields.
    /// </summary>
    public void SetProfile(SandboxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Profile = SandboxProfileCopy.Clone(profile);
        MappedFolders.Clear();
        foreach (var folder in Profile.Configuration.MappedFolders)
        {
            MappedFolders.Add(folder);
        }
        Validate();
    }

    /// <summary>
    /// Adds a new mapped folder.
    /// </summary>
    [RelayCommand]
    public void AddMappedFolder()
    {
        var newFolder = new WsbMappedFolder
        {
            HostFolder = string.Empty,
            IsReadOnly = true
        };
        MappedFolders.Add(newFolder);
        Profile.Configuration.MappedFolders.Add(newFolder);
        Validate();
    }

    /// <summary>
    /// Removes a mapped folder.
    /// </summary>
    [RelayCommand]
    public void RemoveMappedFolder(WsbMappedFolder? folder)
    {
        if (folder is null)
        {
            return;
        }

        MappedFolders.Remove(folder);
        Profile.Configuration.MappedFolders.Remove(folder);
        Validate();
    }

    /// <summary>
    /// Validates the current configuration against sandbox specifications.
    /// </summary>
    [RelayCommand]
    public void Validate()
    {
        var result = _validator.Validate(Profile.Configuration);
        HasErrors = !result.IsValid;

        if (HasErrors)
        {
            ValidationSummary = string.Join("; ", result.Messages
                .Where(m => m.Severity == ValidationSeverity.Error)
                .Select(m => $"{m.PropertyName}: {m.Message}"));
        }
        else
        {
            ValidationSummary = "Configuration is valid.";
        }
    }

    /// <summary>
    /// Saves the current profile to persistent storage.
    /// </summary>
    [RelayCommand]
    public async Task<bool> SaveAsync()
    {
        var name = Profile.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            HasErrors = true;
            ValidationSummary = "A profile name is required.";
            return false;
        }

        if (name.Length > 120)
        {
            HasErrors = true;
            ValidationSummary = "A profile name cannot exceed 120 characters.";
            return false;
        }

        if ((Profile.Description?.Length ?? 0) > 500)
        {
            HasErrors = true;
            ValidationSummary = "A profile description cannot exceed 500 characters.";
            return false;
        }

        Profile.Name = name;
        Validate();
        if (HasErrors)
        {
            return false;
        }

        await _repository.SaveAsync(Profile);
        return true;
    }
}
