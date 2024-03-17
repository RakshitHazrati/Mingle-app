using Microsoft.Maui.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Security.Cryptography;
namespace MauiApp1
{
    public partial class LogIn : ContentPage
    {
        private readonly MongoDbContext _dbContext;
        public LogIn(MongoDbContext dbContext)
        {
            InitializeComponent();
            _dbContext = dbContext;
        }

        public void SwichToSignUp(object sender, EventArgs e)
        {
            var signUpPage = Handler.MauiContext.Services.GetService<SignUpPage>();
            Navigation.PushAsync(signUpPage);
        }

        private async void LogInClicked(object sender, EventArgs e)
        {

            //check login and send to next page 
          
            if(enteredEmail.Text==string.Empty)
            {
                ErrorMessage.Text = "Email cannot be empty";
                return;
            }
            if (PasswordEntry.Text == string.Empty)
            {
                ErrorMessage.Text = "Password cannot be empty";
                return;
            }
            if (ConditionTextBox.IsChecked == false)
            {
                ErrorMessage.Text = "Accept terms and conditions";
                return;
            }
            User user = await _dbContext.LoginDetailsFetch(enteredEmail.Text);

            if (user != null)
            {
                if (IsPasswordMatch(PasswordEntry.Text, user.PasswordHash)){
                    UserJwtTocken.JwtTocken = user.JwtTocken;
                    var myDatesPage = Handler.MauiContext.Services.GetService<MyDatesPage>();
                    Navigation.PushAsync(myDatesPage);
                    /*var myCupidInvite = Handler.MauiContext.Services.GetService<PlayCupidInvitePage>();
                    Navigation.PushAsync(myCupidInvite);*/
                }
                else
                {
                    ErrorMessage.Text = "Incorrect email or password.";
                }
            }
            else
            {
                ErrorMessage.Text = "Incorrect email or password.";
            }
        }

        private bool IsPasswordMatch(String enteredPassword, String userPassword)
        {
            var hashPass = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(enteredPassword));
            string hashPassString = BitConverter.ToString(hashPass).Replace("-", "").ToLower();

           return hashPassString == userPassword;

        }


        private void OnShowButtonPressed(object sender, EventArgs e)
        {
            if (enteredEmail.IsPassword)
            {
                enteredEmail.IsPassword = false; 

            }
            else
            {
                enteredEmail.IsPassword=true;

            }
        }
    }

}