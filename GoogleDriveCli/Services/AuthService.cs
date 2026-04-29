using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

namespace GoogleDriveCli.Services;

public static class AuthService
{
    private static readonly string[] Scopes = { Google.Apis.Drive.v3.DriveService.Scope.Drive };
    private const string ApplicationName = "GoogleDriveCli";

    public static UserCredential GetCredential()
    {
        using var stream = new FileStream("client_secret.json", FileMode.Open, FileAccess.Read);
        var credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".google-drive-cli-token");
        return GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(credPath, true)
        ).Result;
    }

    public static Google.Apis.Drive.v3.DriveService GetDriveService()
    {
        var credential = GetCredential();
        return new Google.Apis.Drive.v3.DriveService(new Google.Apis.Services.BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
    }
}