using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyScavenger.Interfaces;
using SpotifyScavenger.Models;
using SpotifyScavenger.TrackSources;
using System.Text.RegularExpressions;
using System.Collections;
using static SpotifyAPI.Web.PlaylistRemoveItemsRequest;
using static System.Formats.Asn1.AsnWriter;
using System.Net;

namespace SpotifyScavenger
{
  public class Program
  {
    public static void Main(string[] args)
    {
      try
      {
        RetrieveTrackBot mainLoop = new RetrieveTrackBot();
        mainLoop.MainLoop().GetAwaiter().GetResult();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.StackTrace);
      }
    }
  }
  public class RetrieveTrackBot
  {
    private string playlistCode = Environment.GetEnvironmentVariable("SPOTIFY_PLAYLIST_ID");
    private SpotifyClient spotify;

    DateTime currentDate;


    bool checkForDate = true;

    public async Task<bool> MainLoop()
    {
      await Authenticate();

      List<TrackData> data = GetDataFromSources();

      if (!data.Any())
      {
        throw new Exception("No data retrieved from sources");
      }

      List<TrackData> hardstyleData = new List<TrackData>();
      hardstyleData.AddRange(data.Where(item => item.Genre == Genre.Hardstyle));
      hardstyleData = hardstyleData.DistinctBy(item => item.TrackName).ToList();

      #region clean hardstyle list
      List<PlaylistTrack<FullTrack>> currentItemsInPlaylistHardstyle = await GetListOfCurrentItemsInPlaylistHardstyle();

      hardstyleData = await DeleteTracksWeAlreadyHave(currentItemsInPlaylistHardstyle, hardstyleData);

      List<PlaylistRemoveItemsRequest.Item> itemsToDeleteHardstyle = new List<Item>();
      foreach (PlaylistTrack<FullTrack> item in currentItemsInPlaylistHardstyle)
      {
        var result = await DeleteTracksOlderThanXDays(item);
        if (result != null)
        {
          itemsToDeleteHardstyle.Add(result);
        }
      }

      await DeleteEntriesFromSpotifyPlaylistHardstyle(itemsToDeleteHardstyle);
      #endregion

      currentDate = GetProperTimeZone();
      Console.WriteLine($"Searching for tracks with the following date: {currentDate.Day}-{currentDate.Month}-{currentDate.Year}");

      data = new List<TrackData>();
      data.AddRange(hardstyleData);

      //data.Reverse();
      int maximumAmountOfFails = 30;
      int currentlyFailed = 0;

      foreach (TrackData item in data)
      {
        try
        {
          bool trackFound = false;
          List<string> artists = GetListOfArtistsFromString(item);

          Thread.Sleep(1000);
          var result222 = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, item.TrackName + " " + item.ArtistName));

          if (result222.Tracks.Items.Any())
          {
            FullTrack foundTrack = null;
            if (checkForDate)
            {
              int year = currentDate.Year;
              int day = currentDate.Day;
              int month = currentDate.Month;


              foreach (var track in result222.Tracks.Items)
              {
                if (DateTime.TryParse(track.Album.ReleaseDate, out DateTime value))
                {
                  if (value.Year == year && value.Month == month && value.Day == day)
                  {
                    foundTrack = track;
                    break;
                  }
                }
              }
            }
            else
            {
              foundTrack = result222.Tracks.Items.FirstOrDefault();
            }

            if (foundTrack != null)
            {
              foreach (string artist in artists)
              {
                SimpleArtist bestMatch = null;
                if (foundTrack != null)
                {
                  bestMatch = foundTrack.Artists.FirstOrDefault(item => item.Name.ToLower().Trim() == artist.ToLower().Trim());
                }
                if (bestMatch == null)
                {
                  bestMatch = CheckForWildCardMatch(artist, foundTrack.Artists);
                }

                if (bestMatch != null)
                {
                  Console.WriteLine($"Found track: {foundTrack.Uri}");

                  await AddTrackToPlaylist(foundTrack.Uri);

                  trackFound = true;
                  break;
                }
              }
            }
          }

          if (trackFound)
          {
            continue;
          }


          Thread.Sleep(1000);
          var result223 = await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Track, item.TrackName + " " + artists.FirstOrDefault()));

          if (result223.Tracks.Items.Any())
          {
            FullTrack foundTrack = null;
            if (checkForDate)
            {
              int year = currentDate.Year;
              int day = currentDate.Day;
              int month = currentDate.Month;


              foreach (var track in result223.Tracks.Items)
              {
                if (DateTime.TryParse(track.Album.ReleaseDate, out DateTime value))
                {
                  if (value.Year == year && value.Month == month && value.Day == day)
                  {
                    foundTrack = track;
                    break;
                  }
                }
              }
            }
            else
            {
              foundTrack = result223.Tracks.Items.FirstOrDefault();
            }

            if (foundTrack != null)
            {
              foreach (string artist in artists)
              {
                SimpleArtist bestMatch = null;
                if (foundTrack != null)
                {
                  bestMatch = foundTrack.Artists.FirstOrDefault(item => item.Name.ToLower().Trim() == artist.ToLower().Trim());
                }
                if (bestMatch == null)
                {
                  bestMatch = CheckForWildCardMatch(artist, foundTrack.Artists);
                }

                if (bestMatch != null)
                {
                  Console.WriteLine($"Found track: {foundTrack.Uri}");

                  await AddTrackToPlaylist(foundTrack.Uri);

                  trackFound = true;
                  break;
                }
              }
            }
          }

          if (trackFound)
          {
            continue;
          }

          foreach (string artist in artists)
          {
            FullArtist artistFound = await SearchForArtist(artist);
            Thread.Sleep(1000);
            if (artistFound == null) continue;
            List<SimpleAlbum> listOfAlbumsFromArtist = await GetListOfAlbumsFromArtist(artistFound.Id);
            Thread.Sleep(1000);
            SimpleAlbum albumOfToday = await GetAlbumOfTodayFromListOfAlbums(listOfAlbumsFromArtist);

            if (albumOfToday == null) continue;
            Thread.Sleep(1000);

            var tracksOfAlbum = await spotify.Albums.GetTracks(albumOfToday.Id);

            foreach (var track in tracksOfAlbum.Items)
            {
              if (track.Name.ToLower().Trim() == item.TrackName.ToLower().Trim())
              {

                await AddTrackToPlaylist(track.Uri);

                Console.WriteLine($"Found a match: {track.Name} with {item.TrackName}");
                trackFound = true;
                break;
              }
              else
              {
                string result = await GetTrackUriWildCatchMatch(item.TrackName, track);
                if (result != null)
                {
                  await AddTrackToPlaylist(track.Uri);
                  Console.WriteLine($"Found a match: {track.Name} with {item.TrackName}");
                  trackFound = true;
                  break;
                }
              }
            }


            if (trackFound)
            {
              break;
            }

          }

          if (trackFound)
          {
            continue;
          }

          if (!trackFound)
          {
            FullArtist artistFound = await SearchForArtist(item.ArtistName);
            Thread.Sleep(1000);
            if (artistFound == null)
            {
              Console.WriteLine($"Track not found: {item.TrackName} - {item.ArtistName} for date {currentDate.Day}-{currentDate.Month}-{currentDate.Year}");
              continue;
            }
            List<SimpleAlbum> listOfAlbumsFromArtist = await GetListOfAlbumsFromArtist(artistFound.Id);
            Thread.Sleep(1000);
            SimpleAlbum albumOfToday = await GetAlbumOfTodayFromListOfAlbums(listOfAlbumsFromArtist);

            if (albumOfToday == null)
            {
              Console.WriteLine($"Track not found: {item.TrackName} - {item.ArtistName} for date {currentDate.Day}-{currentDate.Month}-{currentDate.Year}");
              continue;
            }
            Thread.Sleep(1000);

            var tracksOfAlbum = await spotify.Albums.GetTracks(albumOfToday.Id);

            foreach (var track in tracksOfAlbum.Items)
            {
              if (track.Name.ToLower().Trim() == item.TrackName.ToLower().Trim())
              {


                await AddTrackToPlaylist(track.Uri);


                Console.WriteLine($"Found a match: {track.Name} with {item.TrackName}");
                trackFound = true;
                break;
              }
              else
              {
                string result = await GetTrackUriWildCatchMatch(item.TrackName, track);
                if (result != null)
                {

                  await AddTrackToPlaylist(track.Uri);


                  Console.WriteLine($"Found a match: {track.Name} with {item.TrackName}");
                  trackFound = true;
                  break;
                }
              }
            }
          }

          if (!trackFound)
          {
            Console.WriteLine($"Track not found: {item.TrackName} - {item.ArtistName}");
          }

        }
        catch (Exception ex)
        {
          Console.WriteLine($"Got an error, gonna retry in 60 seconds: {ex.Message}");
          Thread.Sleep(60000);
          currentlyFailed += 1;
          if (currentlyFailed >= maximumAmountOfFails)
          {
            throw new Exception($"Failed a total of {currentlyFailed} times, stopping");
          }
        }
      }

      return false;
    }

    public List<TrackData> GetDataFromSources()
    {
      List<IScavenge> listOfSources = new List<IScavenge>();
      listOfSources.Add(new HardstyleDotComScavengerHardstyle());

      List<TrackData> trackData = new List<TrackData>();
      foreach (IScavenge scavengeSource in listOfSources)
      {
        trackData.AddRange(scavengeSource.ScavengeForTracks());
      }

      return trackData;
    }

    public async Task AddTrackToPlaylist(string trackUri)
    {
      var request = new PlaylistAddItemsRequest(new List<string> { trackUri });
      request.Position = 0;

      const string prefix = "spotify:track:";
      int index = trackUri.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);

      var trackId = trackUri.Substring(index + prefix.Length);

      var track = await spotify.Tracks.Get(trackId);

      // Access the artist information
      var artists = track.Artists;

      // Print information about the artists
      foreach (var i in artists)
      {
        var artist = await spotify.Artists.Get(i.Id);
        var followers = artist.Followers.Total;
        var popularity = artist.Popularity;

        if (followers > 250 && popularity > 10)
        {
          var addedItems = await spotify.Playlists.AddItems(playlistCode, request);
          break;
        }
        else
        {
          Console.WriteLine($"Artist is below threshold: Followers: 250 ({followers}), Popularity: 10 ({popularity})");
        }
      }
    }

    public async Task Authenticate()
    {
      var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
      var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
      var redirectUri = new Uri(Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI"));
      var refreshToken = Environment.GetEnvironmentVariable("SPOTIFY_REFRESH_TOKEN");

      try
      {
        Console.WriteLine("Trying to login via refresh token...");
        AuthorizationCodeRefreshResponse response = await new OAuthClient().RequestToken(new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken));
        spotify = new SpotifyClient(response.AccessToken);

        Console.WriteLine($"Access Token: {response.AccessToken}");
        Console.WriteLine($"Refresh Token: {refreshToken}");
      }
      catch (Exception ex)
      {
        Console.WriteLine("Trying to login via new request...");
        // Authorization Code Flow
        var request = new LoginRequest(
            redirectUri,
            clientId,
            LoginRequest.ResponseType.Code
        )
        {
          Scope = new[] { Scopes.UserReadPrivate, Scopes.PlaylistReadPrivate, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPublic } // Include necessary scopes
        };

        var uri = request.ToUri();
        Console.WriteLine($"Please navigate to {uri} and log in.");

        Console.Write("Enter the code from the redirect URI: ");
        var code = Console.ReadLine();

        var response = await new OAuthClient().RequestToken(
            new AuthorizationCodeTokenRequest(
                clientId,
                clientSecret,
                code,
                redirectUri
            )
        );

        // Use the obtained access token and refresh token
        Environment.SetEnvironmentVariable("SPOTIFY_REFRESH_TOKEN", response.RefreshToken);

        spotify = new SpotifyClient(response.AccessToken);

        Console.WriteLine($"Access Token: {response.AccessToken}");
        Console.WriteLine($"Refresh Token: {response.RefreshToken}");
      }
    }

    public async Task<List<PlaylistTrack<FullTrack>>> GetListOfCurrentItemsInPlaylistHardstyle()
    {
      Paging<PlaylistTrack<IPlayableItem>>? currPlaylist = await spotify.Playlists.GetItems(playlistCode);
      List<PlaylistTrack<IPlayableItem>> itemsToAppend = new List<PlaylistTrack<IPlayableItem>>();
      await foreach (var item in spotify.Paginate(currPlaylist))
      {
        itemsToAppend.Add(item);
        // you can use "break" here!
      }


      var serializeMe = JsonConvert.SerializeObject(itemsToAppend);
      List<PlaylistTrack<FullTrack>> itemz = JsonConvert.DeserializeObject<List<PlaylistTrack<FullTrack>>>(serializeMe);
      return itemz;
    }

    public async Task<List<TrackData>> DeleteTracksWeAlreadyHave(List<PlaylistTrack<FullTrack>> itemsToPossiblyDelete, List<TrackData> itemsRetrievedFromSources)
    {
      foreach (PlaylistTrack<FullTrack> itemToPossiblyDelete in itemsToPossiblyDelete)
      {
        TrackData itemAlreadyExists = itemsRetrievedFromSources.FirstOrDefault(item => itemToPossiblyDelete.Track?.Name.ToLower().Trim().Contains(item.TrackName.ToLower().Trim()) == true);
        if (itemAlreadyExists != null)
        {
          itemsRetrievedFromSources.Remove(itemAlreadyExists);
          continue;
        }
        else
        {
          //double check!
          TrackData possibleMatch = CheckForWildCardMatch(itemToPossiblyDelete.Track?.Name, itemsRetrievedFromSources);
          if (possibleMatch != null)
          {
            itemsRetrievedFromSources.Remove(possibleMatch);
            continue;
          }
        }
        Console.WriteLine($"Unable to find track in playlist: {itemToPossiblyDelete.Track.Name} - {itemToPossiblyDelete.Track.Artists.FirstOrDefault().Name}");
      }

      return itemsRetrievedFromSources;
    }

    public async Task<PlaylistRemoveItemsRequest.Item> DeleteTracksOlderThanXDays(PlaylistTrack<FullTrack> item)
    {
      if (item.AddedAt < DateTime.Now.AddDays(-7))
      {
        return new PlaylistRemoveItemsRequest.Item { Uri = item.Track.Uri };
      }
      return null;
    }

    public async Task DeleteEntriesFromSpotifyPlaylistHardstyle(List<PlaylistRemoveItemsRequest.Item> itemsToRemove)
    {
      await spotify.Playlists.RemoveItems(playlistCode, new PlaylistRemoveItemsRequest { Tracks = itemsToRemove });
    }

    public DateTime GetProperTimeZone()
    {
      TimeZoneInfo NLtimezone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
      DateTime dateNow = DateTime.UtcNow;

      return TimeZoneInfo.ConvertTimeFromUtc(dateNow, NLtimezone);
    }


    public List<string> GetListOfArtistsFromString(TrackData track)
    {
      //List of strings to split by
      String[] delimiters = { " & ", " and ", " featuring ", " features ", ",", " ft. " };

      List<string> artists = track.ArtistName.Split(delimiters, StringSplitOptions.None).ToList();

      return artists;
    }


    public async Task<FullArtist> SearchForArtist(string artistName)
    {
      var searchRequest = new SearchRequest(SearchRequest.Types.Artist, artistName);
      var response = await spotify.Search.Item(searchRequest);

      FullArtist bestMatch = null;
      if (response != null)
      {
        bestMatch = response.Artists.Items.FirstOrDefault(item => item.Name.ToLower().Trim() == artistName.ToLower().Trim());
      }
      if (bestMatch == null)
      {
        bestMatch = CheckForWildCardMatch(artistName, response.Artists.Items);
      }

      return bestMatch;
    }

    public async Task<List<SimpleAlbum>> GetListOfAlbumsFromArtist(string artistId)
    {
      List<SimpleAlbum> albumsRetrieved = new List<SimpleAlbum>();

      var albums = await spotify.Artists.GetAlbums(artistId);
      await foreach (var item in spotify.Paginate(albums))
      {
        albumsRetrieved.Add(item);
        // you can use "break" here!
      }

      return albumsRetrieved;
    }

    public async Task<SimpleAlbum> GetAlbumOfTodayFromListOfAlbums(List<SimpleAlbum> albums)
    {
      SimpleAlbum foundAlbum = null;
      if (checkForDate)
      {
        int year = currentDate.Year;
        int month = currentDate.Month;
        int day = currentDate.Day;

        foreach (var album in albums)
        {
          if (DateTime.TryParse(album.ReleaseDate, out DateTime releasedate))
          {
            if (releasedate.Year == year && releasedate.Month == month && releasedate.Day == day)
            {
              foundAlbum = album;
              break;
            }
          }
        }
      }
      else
      {
        foundAlbum = albums.FirstOrDefault();
      }
      return foundAlbum;
    }

    public SimpleArtist CheckForWildCardMatch(string artistName, List<SimpleArtist> artistsRetrieved)
    {
      Regex rgx = new Regex("[^a-zA-Z0-9 -']");
      var newString = rgx.Replace(artistName.ToLower().Trim(), "");
      newString = newString.Replace("  ", " ");
      List<string> indivialWordsSpotifyTrack = newString.Split(" ").ToList();
      foreach (SimpleArtist artistResult in artistsRetrieved.ToList())
      {
        string possibleItem = rgx.Replace(artistResult.Name.ToLower().Trim(), " ");
        List<string> individualWords = possibleItem.Split(" ").ToList();
        if (indivialWordsSpotifyTrack.Intersect(individualWords).Count() == indivialWordsSpotifyTrack.Count())
        {
          //perfect match
          return artistResult;
        }
      }

      return null;
    }

    public FullArtist CheckForWildCardMatch(string artistName, List<FullArtist> artistsRetrieved)
    {
      Regex rgx = new Regex("[^a-zA-Z0-9 -']");
      var newString = rgx.Replace(artistName.ToLower().Trim(), "");
      newString = newString.Replace("  ", " ");
      List<string> indivialWordsSpotifyTrack = newString.Split(" ").Distinct().ToList();
      foreach (FullArtist artistResult in artistsRetrieved.ToList())
      {
        string possibleItem = rgx.Replace(artistResult.Name.ToLower().Trim(), "");
        List<string> individualWords = possibleItem.Split(" ").Distinct().ToList();
        if (indivialWordsSpotifyTrack.Intersect(individualWords).Count() == indivialWordsSpotifyTrack.Count())
        {
          //perfect match
          return artistResult;
        }
      }

      return null;
    }

    public async Task<string> GetTrackUriWildCatchMatch(string nameOfTrack, SimpleTrack item)
    {
      Regex rgx = new Regex("[^a-zA-Z0-9 -]");
      var itemName = item.Name.ToLower().Trim().Replace('´', '\'');
      var newString = rgx.Replace(item.Name.ToLower().Trim(), "");
      newString = newString.Replace("  ", " ");
      List<string> indivialWordsSpotifyTrack = newString.Split(" ").Distinct().ToList();

      string possibleItem = rgx.Replace(nameOfTrack.ToLower().Trim(), "");
      List<string> individualWords = possibleItem.Split(" ").Distinct().ToList();
      if (indivialWordsSpotifyTrack.Intersect(individualWords).Count() == indivialWordsSpotifyTrack.Count())
      {
        return item.Uri;
      }

      return null;
    }


    public TrackData CheckForWildCardMatch(string trackName, List<TrackData> itemsWeRetrieved)
    {
      Regex rgx = new Regex("[^a-zA-Z0-9 -']");
      var newString = rgx.Replace(trackName.ToLower().Trim(), "");
      newString = newString.Replace("  ", " ");
      List<string> indivialWordsSpotifyTrack = newString.Split(" ").Distinct().ToList();

      indivialWordsSpotifyTrack = indivialWordsSpotifyTrack.Select(s => s.Trim()).ToList();

      foreach (TrackData trackDataItem in itemsWeRetrieved.ToList())
      {
        string possibleItem = rgx.Replace(trackDataItem.TrackName.ToLower().Trim(), "");
        List<string> individualWords = possibleItem.Split(" ").Distinct().ToList();

        individualWords = individualWords.Select(s => s.Trim()).ToList();

        if (indivialWordsSpotifyTrack.Intersect(individualWords).Count() == indivialWordsSpotifyTrack.Count())
        {
          //perfect match
          return trackDataItem;
        }
      }

      return null;
    }
  }
}