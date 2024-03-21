namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        //private MongoDbContext dbContext;
        //private string mobileNumber;

        public MainPage()
        {
            InitializeComponent();

            //string connectionString = "mongodb://rakshithazrati:w8mSxGhX4sbnOID5@ac-annsyke-shard-00-02.7qncxbg.mongodb.net:27017,ac-annsyke-shard-00-01.7qncxbg.mongodb.net:27017,ac-annsyke-shard-00-00.7qncxbg.mongodb.net:27017/MingleApp?authSource=admin&ssl=true\", \"MingleApp";
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
