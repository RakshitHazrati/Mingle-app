namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        //private MongoDbContext dbContext;
        //private string mobileNumber;

        public MainPage()
        {
            InitializeComponent();

            // Legacy direct database access was removed. The rebuilt client calls Mingle.Api.
           // string databaseName = "MingleApp";
            //dbContext = new MongoDbContext(connectionString, databaseName);
        }

        private void OnNextClicked(object sender, EventArgs e)
        {
            var LoginPage = Handler.MauiContext.Services.GetService<LogIn>();
            //mobileNumber = enteredPhoneNumber.Text;
            Navigation.PushAsync(LoginPage);
        }
    }

}
