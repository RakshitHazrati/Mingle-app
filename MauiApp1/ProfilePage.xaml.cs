using MongoDB.Bson;
using System.Collections;
using System.Diagnostics;
using System.Text;
namespace MauiApp1;

public partial class ProfilePage : ContentPage
{
    private List<Image> uploadedImages = new List<Image>();
    private List<Byte[]> uploadedImagesByte = new List<byte[]>();
    private MongoDbContext dbContext;
    private string mobileNumber;

    public ProfilePage(MongoDbContext dbContext)
	{
		InitializeComponent();
        this.dbContext = dbContext;
        //this.mobileNumber = mobileNumber;
	}

    private async void OnUploadImageClicked(object sender, EventArgs e)
    {
        var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
        {
            Title = "Select a Photo"
        });
        byte[] imageData;
        string localFilePath = Path.Combine(FileSystem.CacheDirectory, result.FileName);
        using Stream stream = await result.OpenReadAsync();
        using (FileStream localFileStream = File.OpenWrite(localFilePath))
        {

            await stream.CopyToAsync(localFileStream);
        }
        imageData = File.ReadAllBytes(localFilePath);
        Debug.WriteLine("B : " + imageData.Length);
        /*if (result != null)
        {
            // Create an Image element and display the uploaded image
            Image image = new Image
            {
                Source = result.FullPath,
                WidthRequest = 50,
                HeightRequest = 50,
            };

            uploadedImages.Add(image);
            UpdateImageDisplay();

        }*/
        uploadedImagesByte.Add(imageData);
        UpdateImageDisplay();

    }

    private void UpdateImageDisplay()
    {
        // Display up to 4 uploaded images
        for (int i = 0; i < uploadedImagesByte.Count && i < 4; i++)
        {
            switch (i)
            {
                case 0:
                  
                    image1.Source = ImageSource.FromStream(() => new MemoryStream(uploadedImagesByte[i]));
                    break;
                case 1:
                   
                    image2.Source = ImageSource.FromStream(() => new MemoryStream(uploadedImagesByte[i]));
                    break;
                case 2:
                    image3.Source = ImageSource.FromStream(() => new MemoryStream(uploadedImagesByte[i]));
                    break;
                case 3:
                    image4.Source = ImageSource.FromStream(() => new MemoryStream(uploadedImagesByte[i]));
                    break;
            }
        }
    }

    private void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        // Calculate age based on the selected date of birth
        DateTime currentDate = DateTime.Now;
        int age = currentDate.Year - dobPicker.Date.Year - (currentDate.DayOfYear < dobPicker.Date.DayOfYear ? 1 : 0);

        // Update the age label
        ageLabel.Text = $"Age: {age}";
    }

    private void OnSaveProfileClicked(object sender, EventArgs e)
    {
        var userProfile = new UserProfile
        {
            MobileNumber = mobileNumber, 
            FullName = enteredFullName.Text,
            Description = enteredDescription.Text,
            City = enteredCity.Text,
            State = enteredState.Text,
            Country = enteredCountry.Text,
            ZipCode = enteredZipCode.Text,
            DateOfBirth = dobPicker.Date,
        };

        userProfile.Friends.Add("Friend1");
        userProfile.Friends.Add("Friend2");
        string img1 = "";
        string img2 = "";
        string img3 = "";
        string img4 = "";
        for (int i = 0; i < uploadedImagesByte.Count && i < 4; i++)
        {
            switch (i)
            {
                case 0:

                    img1= Encoding.Default.GetString(uploadedImagesByte[i]);
                    break;
                case 1:

                    img2= Encoding.Default.GetString(uploadedImagesByte[i]);
                    break;
                case 2:
                    img3= Encoding.Default.GetString(uploadedImagesByte[i]);
                    break;
                case 3:
                    img4= Encoding.Default.GetString(uploadedImagesByte[i]);
                    break;
            }
        }

        var userImages = new UserImages
        {
            JwtTocken = UserJwtTocken.JwtTocken,
            Image_1 = img1,
            Image_2 = img2,
            Image_3 = img3,
            Image_4 = img4

        };
        dbContext.UserImages.InsertOne(userImages);
        dbContext.UserProfiles.InsertOne(userProfile);
    }
}