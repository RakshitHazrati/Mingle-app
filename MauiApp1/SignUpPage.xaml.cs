using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MauiApp1;

public partial class SignUpPage : ContentPage
{
    private readonly MongoDbContext _dbContext;
    private readonly Random random = new Random();
    //private EmailService emailService;
    
    public SignUpPage(MongoDbContext dbContext)
	{
		InitializeComponent();
        //string connectionString = "mongodb://rakshithazrati:w8mSxGhX4sbnOID5@ac-annsyke-shard-00-02.7qncxbg.mongodb.net:27017,ac-annsyke-shard-00-01.7qncxbg.mongodb.net:27017,ac-annsyke-shard-00-00.7qncxbg.mongodb.net:27017/MingleApp?authSource=admin&ssl=true";
        ////string connectionString = "mongodb+srv://rakshithazrati:w8mSxGhX4sbnOID5@mingleapp.7qncxbg.mongodb.net/?retryWrites=true&w=majority&appName=MingleApp";
        //string databaseName = "MingleApp";
        //dbContext = new MongoDbContext(connectionString, databaseName);
        _dbContext = dbContext;
    }

    private void SwichToSignIn(object sender, EventArgs e)
    {
        var LoginPage = Handler.MauiContext.Services.GetService<LogIn>();
        Navigation.PushAsync(LoginPage);
    }

    public string GenerateOTP()
    {
        return random.Next(100000,999999).ToString();
    }
    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        ErrorMessage.Text = "";
        if (enteredUsername.Text == string.Empty)
        {
            ErrorMessage.Text = "Username cannot be empty";
            return;
        }
        if (await IsUserNameExists(enteredUsername.Text))
        {
            ErrorMessage.Text = "Username already exists.";
            return;
        }
        if (enteredEmail.Text == string.Empty)
        {
            ErrorMessage.Text = "Email cannot be empty";
            return;
        }
        if (await IsEmailExists(enteredEmail.Text))
        {
            ErrorMessage.Text = "Email already exists.";
            return;
        }
        if (!IsEmailValid(enteredEmail.Text))
        {
            ErrorMessage.Text = "Email Entered Incorrect.";
            return;
            
        }
        if (enteredPhoneNumber.Text == string.Empty)
        {
            ErrorMessage.Text = "Phone number cannot be empty";
            return;
        }
        if (enteredPassword.Text == string.Empty)
        {
            ErrorMessage.Text = "Password cannot be empty";
            return;
        }
        if (IsPasswordValid(enteredPassword.Text))
        {
            ErrorMessage.Text = "";
            var emailService = new EmailService();
            string otp = GenerateOTP();
            emailService.SendOTPMail("rakshit@yugsha.com", otp); //change to entered email 
            var jwtService = new JwtService();
            string token = jwtService.GenerateToken(enteredUsername.Text);
            Console.WriteLine(token);
            await _dbContext.SaveSignUpDetails(enteredUsername.Text, enteredEmail.Text, enteredPhoneNumber.Text, enteredPassword.Text,token); //add verified = false and make it true when otp verification done
        
            var otpPage = Handler.MauiContext.Services.GetService<OtpPage>();
            await Navigation.PushAsync(otpPage);
        }





    }
    async Task<bool> IsUserNameExists(string username)
    {
        return await _dbContext.IsUserNameExistsFetch(username);
    }
    async Task<bool> IsEmailExists(string email)
    {
        return await _dbContext.IsEmailExistsFetch(email) ;
    }

    bool IsEmailValid(string email)
    {
        // Use a regular expression for basic email format validation
        string emailPattern = @"^[a-zA-Z0-9._-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,4}$";
        Regex regex = new Regex(emailPattern);

        return regex.IsMatch(email);
    }
     bool IsPasswordValid(string password)
    {
        // Define password criteria
        int minLength = 8;
        string specialCharacters = @"!@#$%^&*";
        Regex digitRegex = new Regex(@"[0-9]");
        Regex uppercaseRegex = new Regex(@"[A-Z]");
        Regex lowercaseRegex = new Regex(@"[a-z]");

        // Check length
        if (password.Length < minLength)
        {

            ErrorMessage.Text = "Password length is short.";
            return false;
        }

        // Check for at least one digit
        if (!digitRegex.IsMatch(password))
        {

            ErrorMessage.Text = "Password should contain atleast one digit.";
            return false;
        }

        // Check for at least one uppercase letter
        if (!uppercaseRegex.IsMatch(password))
        {

            ErrorMessage.Text = "Password should contain atleast one uppercase letter.";
            return false;
        }

        // Check for at least one lowercase letter
        if (!lowercaseRegex.IsMatch(password))
        {

            ErrorMessage.Text = "Password should contain atleast one lowercase letter.";
            return false;
        }

        // Check for at least one special character
        if (!password.Any(c => specialCharacters.Contains(c)))
        {

            ErrorMessage.Text = "Password should contain atleast one special character.";
            return false;
        }

        // All criteria met
        return true;
    }
}