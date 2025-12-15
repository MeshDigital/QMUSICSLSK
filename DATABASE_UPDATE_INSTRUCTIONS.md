# Database Schema Update Instructions

## Issue
The database is missing new columns: `IsLiked`, `Rating`, `PlayCount`, `LastPlayedAt`

## Solution
Delete the old database file to allow EF Core to create a new one with the correct schema.

### Steps:

1. **Close the application** if it's running

2. **Delete the database file**:
   ```
   Location: %AppData%\SLSKDONET\library.db
   Full path: C:\Users\quint\AppData\Roaming\SLSKDONET\library.db
   ```

3. **Run the application again**:
   ```powershell
   dotnet run
   ```

4. **EF Core will automatically create a new database** with all the correct columns

### What You'll Lose
- Existing playlist data
- Download history
- Activity logs

### What You'll Keep
- All downloaded music files (they're separate from the database)
- Application settings

### Alternative (If you want to keep data)
Install EF Core tools and run migrations:
```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add AddRatingsAndLikesColumns
dotnet ef database update
```

## After Database Recreation
The application will start fresh with:
- ✅ All new columns (Rating, IsLiked, PlayCount, LastPlayedAt)
- ✅ Smart Playlists working
- ✅ Ratings & Likes functional
- ✅ 0 errors

You can re-import your playlists from Spotify or CSV.
