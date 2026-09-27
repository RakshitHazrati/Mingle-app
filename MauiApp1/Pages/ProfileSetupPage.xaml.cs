using MauiApp1.Services;

namespace MauiApp1.Pages;

public partial class ProfileSetupPage : ContentPage
{
    private readonly AppState _state = AppServices.Get<AppState>();
    private readonly bool _isOnboarding;
    private byte[]? _photoBytes;
    private string? _photoContentType;

    public ProfileSetupPage(bool isOnboarding)
    {
        InitializeComponent();
        _isOnboarding = isOnboarding;
        StepLabel.Text = isOnboarding ? "STEP 1 OF 2" : "EDIT PROFILE";
        SaveButton.Text = isOnboarding ? "Save and choose contacts" : "Save profile";
        BirthDatePicker.MaximumDate = DateTime.Today.AddYears(-18);
        BirthDatePicker.MinimumDate = DateTime.Today.AddYears(-100);
        BirthDatePicker.Date = _state.BirthDate?.LocalDateTime.Date ?? DateTime.Today.AddYears(-25);
        NameEntry.Text = _state.CurrentUserName;
        CityEntry.Text = _state.City;
        OccupationEntry.Text = _state.Occupation;
        BioEditor.Text = _state.Bio;
        AvatarInitials.Text = GetInitials(_state.CurrentUserName);
        if (!string.IsNullOrWhiteSpace(_state.ProfileImagePath) && File.Exists(_state.ProfileImagePath))
        {
            ProfileImage.Source = ImageSource.FromFile(_state.ProfileImagePath);
            ProfileImage.IsVisible = true;
            AvatarInitials.IsVisible = false;
        }
    }

    private async void OnChoosePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Choose your Mingle profile photo" });
            if (result is null) return;
            var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
            _photoContentType = extension == ".png" ? "image/png" : extension is ".jpg" or ".jpeg" ? "image/jpeg" : null;
            if (_photoContentType is null)
            {
                ShowError("Choose a JPEG or PNG image.");
                return;
            }
            await using var stream = await result.OpenReadAsync();
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            if (memory.Length > 5_000_000)
            {
                ShowError("Choose a profile photo smaller than 5 MB.");
                return;
            }
            _photoBytes = memory.ToArray();
            ProfileImage.Source = ImageSource.FromStream(() => new MemoryStream(_photoBytes));
            ProfileImage.IsVisible = true;
            AvatarInitials.IsVisible = false;
            ErrorLabel.IsVisible = false;
        }
        catch (Exception ex)
        {
            ShowError($"Couldn’t open that photo: {ex.Message}");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim() ?? "";
        var city = CityEntry.Text?.Trim() ?? "";
        var occupation = OccupationEntry.Text?.Trim() ?? "";
        var bio = BioEditor.Text?.Trim() ?? "";
        if (name.Length < 2 || city.Length < 2 || occupation.Length < 2 || bio.Length < 10)
        {
            ShowError("Complete every field and write at least 10 characters about yourself.");
            return;
        }

        SaveButton.IsEnabled = false;
        SaveButton.Text = "Saving...";
        try
        {
            var api = AppServices.Get<MingleApiClient>();
            var user = await api.UpdateProfileAsync(
                name, BirthDatePicker.Date, city, occupation, bio);
            if (user is null) throw new HttpRequestException("The profile response was empty.");
            if (_photoBytes is not null && _photoContentType is not null)
            {
                await api.UploadProfilePhotoAsync(_photoBytes, _photoContentType);
                var extension = _photoContentType == "image/png" ? ".png" : ".jpg";
                var localPath = Path.Combine(FileSystem.AppDataDirectory, "my-profile" + extension);
                await File.WriteAllBytesAsync(localPath, _photoBytes);
                _state.SetProfilePhotoPath(localPath);
                user = user with { HasProfilePhoto = true };
            }
            _state.ApplyCurrentUser(user);
            if (_isOnboarding)
            {
                await Navigation.PushAsync(new ContactsOnboardingPage());
            }
            else
            {
                await Navigation.PopAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex is HttpRequestException or TaskCanceledException
                ? ex.Message
                : "We couldn’t save your profile. Please try again.");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = _isOnboarding ? "Save and choose contacts" : "Save profile";
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private static string GetInitials(string name) => string.Join("", name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
}
