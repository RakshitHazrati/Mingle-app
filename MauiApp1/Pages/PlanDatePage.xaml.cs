using MauiApp1.Models;

namespace MauiApp1.Pages;

public partial class PlanDatePage : ContentPage
{
    private readonly PersonCard _person;

    public PlanDatePage(PersonCard person)
    {
        InitializeComponent();
        _person = person;
        BindingContext = person;
        DatePicker.MinimumDate = DateTime.Today;
        DatePicker.Date = DateTime.Today.AddDays(2);
        VenuePicker.SelectedIndex = 0;
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var venue = VenuePicker.SelectedItem?.ToString() ?? "Coffee and a walk";
        var time = TimePicker.Time.ToString(@"hh\:mm");
        await DisplayAlert("Date idea sent", $"{_person.Name} will see your idea for {DatePicker.Date:ddd, d MMM} at {time}.", "Lovely");
        await Navigation.PopAsync();
    }
}
