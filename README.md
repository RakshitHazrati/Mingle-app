# Mingle

Mingle is a friend-curated dating app built with .NET MAUI and ASP.NET Core. Friends can invite their network, introduce two people they know, and earn points when both people accept. Accepted matches can chat and plan a date.

## Projects

- `MauiApp1` — .NET 9 MAUI client for Android, iOS, Windows, and Mac Catalyst.
- `Mingle.Api` — ASP.NET Core API with MongoDB persistence and an explicit in-memory development mode.

The current product slice includes onboarding, sign up/sign in, Mongo-backed profile setup with authenticated GridFS photos, Android/iOS contact permission, explicit multi-contact selection, phone-hash member discovery, referral sharing, matching two friends from the signed-in user's circle, accepting or declining, polling chat, date planning, invitations, points, and profile/privacy screens.

## Run locally in VS Code

Prerequisites: .NET 9 SDK, the MAUI workload, Android SDK/JDK, and a running Android emulator.

Configure MongoDB as described below, then start the local API from the repository folder:

```powershell
dotnet run --project Mingle.Api\Mingle.Api.csproj --launch-profile http
```

The Android client uses `http://10.0.2.2:5088`, which maps from the standard Android emulator to the development machine. Then build and install the app:

```powershell
dotnet build MauiApp1\MauiApp1.csproj -f net9.0-android -c Debug
adb install -r MauiApp1\bin\Debug\net9.0-android\com.mingleorg.mingle-Signed.apk
adb shell monkey -p com.mingleorg.mingle 1
```

## Deploy the API and create a test APK

The root `Dockerfile` deploys only `Mingle.Api` and listens on port 8080. Configure these secret environment variables with the hosting provider:

```text
Mongo__ConnectionString=<MongoDB Atlas connection string>
Mongo__Database=MingleApp
Storage__UseInMemory=false
```

Do not put the MongoDB connection string in source control or in the mobile app. After deployment, verify `https://<host>/health` reports `"persistence":"mongodb"`, then build the Android APK with that public endpoint:

```powershell
dotnet publish MauiApp1\MauiApp1.csproj -f net9.0-android -c Release `
  -p:MingleApiBaseUrl=https://<host>/
```

Release builds fail when the public endpoint has not been supplied, preventing an emulator-only APK from being shared accidentally.

VS Code does not include Visual Studio's embedded emulator manager, but it can build, install, debug, and launch MAUI apps against any Android Virtual Device started through Android Studio's Device Manager or the Android SDK `emulator` command.

## MongoDB

Never commit a database connection string. For local development, configure it with user secrets:

```powershell
dotnet user-secrets set "Mongo:ConnectionString" "<your MongoDB Atlas connection string>" --project Mingle.Api\Mingle.Api.csproj
```

`Storage:UseInMemory=true` deliberately overrides MongoDB for emulator demos. In-memory data resets whenever the API restarts and must not be used in production.

## Real contact and match test

1. Register at least three accounts with three different phone numbers and complete their profiles.
2. Add two of those phone numbers to the first account's Android device contacts.
3. Sign in as the first account, grant the Android contacts permission, select only those two contacts, and save the circle.
4. Open **Match friends**. Both registered contacts will show **On Mingle** and can be introduced.
5. Sign in as each recipient to accept. Chat becomes available after both accept and refreshes from MongoDB every four seconds while open.

Only explicitly selected contacts are uploaded. Their normalized phone numbers are hashed on-device; MongoDB stores the hash and the owner's private display label, not the raw phone number.

## Messaging providers

SMS and WhatsApp are represented by `IInvitationNotifier`. The development implementation returns a queued placeholder without contacting a paid provider. A production deployment should replace it with server-side adapters such as Twilio Messaging and the WhatsApp Business Cloud API. Provider credentials must stay in a secret manager and must never be embedded in the MAUI app.

## Security baseline

- Passwords use PBKDF2-SHA512 with per-password salt and 210,000 iterations.
- Authentication uses random opaque session tokens; only token hashes are stored server-side.
- Contact phone numbers are normalized and SHA-256 hashed on-device before upload.
- Auth and API endpoints are rate-limited and Mongo sessions have a TTL index.
- Secrets are kept outside source control, Android backup is disabled, and cleartext HTTP is restricted to the emulator development host.
- Messaging and chat authorization are enforced by the API, not trusted to the client.
- A matchmaker can only introduce two registered members whose phone hashes are present in that matchmaker's saved circle.
- Email, phone hash, referral code, and session-token hashes are protected by unique MongoDB indexes; session expiry uses a TTL index.

Before production, add account verification and recovery, abuse reporting/blocking, moderation, push notifications, consent/legal copy, analytics consent, automated tests, CI/CD, a real invite domain, observability, backups, key rotation, and an independent security/privacy review.
