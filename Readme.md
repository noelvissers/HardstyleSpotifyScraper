# Spotify Release Scavenger

This C# console application scrapes music releases from webpages, looks for the corresponding Spotify tracks and adds them to your playlist.

## App settings

This program can be configured via the appsettings.json file. It is recommended to overwrite certain keys via 'user secrets'. 

### Settings
|Setting                  |Type|Details|
|-------------------------|--|--|
|ChechForReleaseDate      |bool|If true, will only link Spotify tracks that were released today, if false, will use the first search result|
|DateAddedThreshold       |int|Days after which a track is removed from the playlist, default 7|
|CheckArtistThreshold     |bool|If true, will only link Spotify tracks that have an artist on them above the followers and popularity threshold set with 'ArtistThresholdFollowers' and 'ArtistThresholdPopularity'|
|ArtistThresholdFollowers |int|Threshold for artists followers, default 250|
|ArtistThresholdPopularity|int|Threshold for artists popularity, default 10||

### Spotify
|Setting                  |Type|Details|
|-------------------------|--|--|
|ClientId|string|Client ID, can be found under 'Basic Information' in your Spotify app|
|ClientSecret|string|Client secret, can be found under 'Basic Information' in your Spotify ap|
|RedirectUri|string|Redirect URI, must be same as the one in your Spotify app |
|RefreshToken [Optional]|string|Refresh token so no login is required every time the program is executed. After first login, the console will print your refresh token that can be used here|

### SpotifyPlaylistId
|Setting                  |Type|Details|
|-------------------------|--|--|
|YOUR_PLAYLIST_NAME|string|Spotify playlist ID, example: '64a2hywOtEHOh2rlClMhpy' |

## Adding your own sources

To add your own source, create a new .cs file in the TrackSources folder with a class that returns a TrackData list. This list should contain tracks with a title, artist, and a hash (used for filtering out duplicates) (see example).
A 'track' in this list with the parameter `track.IsAlbum` set to `true` will be treated as an album when searching on Spotify. In this case all the tracks in the album will be added to the playlist.

Example:
```csharp
namespace SpotifyReleaseScavenger.TrackSources
{
  public class newSource_Source1: IScavenge
  {
    public List<TrackData> GetTracks()
    {
      List<TrackData> trackData = new List<TrackData>();
      
      /* // Your custom scrape code:
       *
       * // Get track title and artist from source
       * //...
       * 
       * // Create track
       * TrackData track = new TrackData();
       * track.Title = //Scraped title
       * track.Artist = //Scraped artist
       * 
       * // Set hash
       * track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{track.Artist}{track.Title}")));
       * 
       * // Add track to list
       * trackData.Add(track);
       */
      
      return trackData;
    }
  }
}
```

In the appsettings.json, add the Spotify playlist ID:
```json
{
 "SpotifyPlaylistId": {
   "NewPlaylist": "SPOTIFY_PLAYLIST_ID"
 }
}
```

In the Run() method in the Program.cs file, add your new playlist like so:
```csharp
// newPlaylist
List<IScavenge> newPlaylistSources = new()
{
    new newSource_Source1()
};
string newPlaylistId = configurationBuilder.GetSection("SpotifyPlaylistId:NewPlaylist").Value;
PlaylistData newPlaylist = new(newPlaylistId, newPlaylistSources);
```
and add the newly created playlist to the list of playlists:
```csharp
// Add playlists to list
List<PlaylistData> playlists = new()
{
  ...
  newPlaylist
};
```
