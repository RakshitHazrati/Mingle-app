using System.Windows.Input;

namespace MauiApp1;

public partial class MyDatesPage : ContentPage
{
    private readonly MongoDbContext _dbContext;
    public ICommand LikeCommand { get; }
    public ICommand DislikeCommand { get; }

    public MyDatesPage(MongoDbContext dbContext)
	{
		InitializeComponent();
        _dbContext = dbContext;
        LikeCommand = new Command(() => OnSwipe("Liked"));
        DislikeCommand = new Command(() => OnSwipe("Disliked"));
    }

    private void OnSwipe(string action)
    {
        // Handle the swipe action (e.g., update the UI or perform some logic)
        Console.WriteLine($"User {action} the item.");
    }
}