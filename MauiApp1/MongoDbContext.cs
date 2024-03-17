using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;

namespace MauiApp1;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(string connectionString, string databaseName)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<UserProfile> UserProfiles =>
        _database.GetCollection<UserProfile>("UserProfiles");

    public IMongoCollection<User> usersCollection =>
    _database.GetCollection<User>("usersCollection");
    public IMongoCollection<UserImages> UserImages =>
    _database.GetCollection<UserImages>("UserImages");

    public async Task SaveSignUpDetails(string username, string email, string phoneNumber, string password, string jwTocken)
    {
        var hashPass = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(password));
        string hashPassString = BitConverter.ToString(hashPass).Replace("-", "").ToLower();

        var newUser = new User
        {
            Username = username,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = hashPassString,
            JwtTocken = jwTocken
        };

        await usersCollection.InsertOneAsync(newUser);
    }

    public async Task SaveProfileImage(string jwtTocken, string image1, string image2, string image3, string image4)
    {
        var newUserImage = new UserImages
        {
            JwtTocken = jwtTocken,
            Image_1 = image1,
            Image_2 = image2,
            Image_3 = image3,
            Image_4 = image4
        };
        await UserImages.InsertOneAsync(newUserImage);
    }

    public async Task<User> LoginDetailsFetch(string email)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Email,email);
        return usersCollection.Find(filter).FirstOrDefault() ;
       
    }
    public async Task<bool> IsUserNameExistsFetch(string username)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Username, username);
        User collection = usersCollection.Find(filter).FirstOrDefault();
        if(collection == null)
        {
            return false;
        }
        return true;

    }
    public async Task<bool> IsEmailExistsFetch(string email)
    {
        var filter = Builders<User>.Filter.Eq(x => x.Username,email);
        User collection= usersCollection.Find(filter).FirstOrDefault();
        if (collection == null)
        {
            return false;
        }
        return true;

    }
}