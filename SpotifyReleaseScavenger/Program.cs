﻿using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;
using SpotifyReleaseScavenger.Interfaces;
using SpotifyReleaseScavenger.Models;
using SpotifyReleaseScavenger.TrackSources;
using System;
using System.Data;
using static SpotifyAPI.Web.PlaylistRemoveItemsRequest;

namespace SpotifyReleaseScavenger
{
  internal class Program
  {
    static void Main(string[] args)
    {
      try
      {
        Console.WriteLine("[Starting SpotifyReleaseScavenger]");
        SpotifyReleaseScavenger spotifyReleaseScavenger = new SpotifyReleaseScavenger();
        spotifyReleaseScavenger.Run().GetAwaiter().GetResult();
        Console.WriteLine("[Done...]");
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
        Console.WriteLine(ex.StackTrace);
      }
    }
  }

  public class SpotifyReleaseScavenger
  {
    private SpotifyClient? spotify;
    public async Task<bool> Run()
    {
      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      // HardstyleReleaseRadar
      List<IScavenge> hardstyleReleaseRadarSources = new()
      {
        new HardstyleDotCom_Albums(),
        new HardstyleDotCom_Tracks(),
        new ArGangDotNl_Albums(),
        new ArGangDotNl_Tracks()
      };
      string hardstyleReleaseRadarId = configurationBuilder.GetSection("SpotifyPlaylistId:HardstyleReleaseRadar").Value;
      PlaylistData hardstyleReleaseRadar = new(hardstyleReleaseRadarId, hardstyleReleaseRadarSources);

      // newPlaylist
      /*
      List<IScavenge> newPlaylistSources = new()
      {
          new newSource_Source1(),
          new newSource_Source2(),
          new newSource_Source3()
      };
      string newPlaylistId = configurationBuilder.GetSection("SpotifyPlaylistId:NewPlaylist").Value;
      PlaylistData newPlaylist = new(newPlaylistId, newPlaylistSources);
      */

      // Add playlists to list
      List<PlaylistData> playlists = new()
      {
        hardstyleReleaseRadar
        //newPlaylist
      };

      await Authenticate();

      //loop through playlists
      foreach (var playlist in playlists)
      {
        // Get new releases from sources
        List<TrackData> trackList = GetTracksFromSources(playlist.Sources);
        if (!trackList.Any())
        {
          Console.WriteLine("No data retrieved from source(s)...");
          break;
        }

        // Set spotify data in list:
        trackList = await MatchWithSpotifyTrack(trackList);

        // Add unique new tracks to playlist
        await AddTracksToPlaylist(trackList, playlist.Id);

        // Remove old tracks from playlist
        await RemoveOldTracksFromPlaylist(playlist.Id);

      }
      return true;
    }

    public async Task Authenticate()
    {
      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      var clientId = configurationBuilder.GetSection("Spotify:ClientId").Value;
      var clientSecret = configurationBuilder.GetSection("Spotify:ClientSecret").Value;
      var redirectUri = new Uri(configurationBuilder.GetSection("Spotify:RedirectUri").Value);
      var refreshToken = configurationBuilder.GetSection("Spotify:RefreshToken").Value;

      try
      {
        Console.WriteLine("[AUTH] Trying to login via refresh token...");
        AuthorizationCodeRefreshResponse response = await new OAuthClient().RequestToken(new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken));
        spotify = new SpotifyClient(response.AccessToken);

        Console.WriteLine($"[AUTH] Access Token: {response.AccessToken}");
        Console.WriteLine($"[AUTH] Refresh Token: {refreshToken}");
      }
      catch (Exception)
      {
        Console.WriteLine("[AUTH] Trying to login via new request...");

        var request = new LoginRequest(
          redirectUri,
          clientId,
          LoginRequest.ResponseType.Code
        )
        {
          Scope = new[] { Scopes.UserReadPrivate, Scopes.PlaylistReadPrivate, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPublic } // Include necessary scopes
        };

        var uri = request.ToUri();
        Console.WriteLine($"[AUTH] Please navigate to {uri} and log in.");
        Console.Write("[AUTH] Enter the code from the redirect URI: ");
        var code = Console.ReadLine();
        var response = await new OAuthClient().RequestToken(
          new AuthorizationCodeTokenRequest(
            clientId,
            clientSecret,
            code,
            redirectUri
        )
        );

        spotify = new SpotifyClient(response.AccessToken);

        Console.WriteLine($"[AUTH] Access Token: {response.AccessToken}");
        Console.WriteLine($"[AUTH] Refresh Token: {response.RefreshToken}");
      }
    }

    public List<TrackData> GetTracksFromSources(List<IScavenge> sources)
    {
      List<TrackData> trackData = new();

      foreach (IScavenge source in sources)
      {
        trackData.AddRange(source.GetTracks());
      }

      trackData = trackData.DistinctBy(track => track.Hash).ToList();

      return trackData;
    }

    public async Task<List<TrackData>> MatchWithSpotifyTrack(List<TrackData> trackList)
    {
      List<TrackData> spotifyTrackList = new();

      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      bool chechForReleaseDate = Convert.ToBoolean(configurationBuilder.GetSection("Settings:ChechForReleaseDate").Value);
      bool checkArtistThreshold = Convert.ToBoolean(configurationBuilder.GetSection("Settings:CheckArtistThreshold").Value);

      foreach (var track in trackList)
      {
        List<string> artists = GetArtistList(track.Artist);
        var searchQuery = $"{track.Title} {artists.First()}";
        SearchRequest searchRequest;

        Console.WriteLine($"[SPOTIFY] Searching for: {artists.First()} - {track.Title}");

        if (track.IsAlbum)
        {
          try
          {
            searchRequest = new SearchRequest(SearchRequest.Types.Album, searchQuery);

            // Get album tracks
            int count = 0;
            int nMaxRetries = 10;
            SearchResponse searchResult;
            while (true)
            {
              try
              {
                searchResult = await spotify.Search.Item(searchRequest);
                break;
              }
              catch (Exception ex)
              {
                if (++count == nMaxRetries)
                {
                  Console.WriteLine($"Error: {ex.Message}");
                }
              }
            }

            if (searchResult.Albums.Items != null)
            {
              var item = searchResult.Albums.Items.First();
              if (chechForReleaseDate)
              {
                DateTime.TryParse(item.ReleaseDate, out DateTime releaseDate);
                if (await CheckReleaseDate(releaseDate))
                {
                  if (!checkArtistThreshold || (checkArtistThreshold && await CheckArtistThreshold(item.Artists)))
                  {
                    // Add album tracks to list
                    var fullAlbum = await spotify.Albums.Get(item.Id);
                    foreach (var albumTrack in fullAlbum.Tracks.Items)
                    {
                      if (albumTrack.Name.Contains("- Extended"))
                      {
                        continue;
                      }

                      TrackData spotifyTrack = new TrackData();
                      var fullAlbumTrack = await spotify.Tracks.Get(albumTrack.Id);

                      spotifyTrack.SpotifyArtists = albumTrack.Artists;
                      spotifyTrack.SpotifyExternalId = fullAlbumTrack.ExternalIds;
                      spotifyTrack.Title = albumTrack.Name;
                      spotifyTrack.Uri = albumTrack.Uri;
                      spotifyTrack.ReleaseDate = releaseDate;
                      spotifyTrackList.Add(spotifyTrack);
                      Console.WriteLine($"[SPOTIFY] Found track: [{spotifyTrack.Uri}] {spotifyTrack.SpotifyArtists.First().Name} - {spotifyTrack.Title}");
                    }
                  }
                }
              }
              else
              {
                // Add album tracks to list
                var fullAlbum = await spotify.Albums.Get(item.Id);
                foreach (var albumTrack in fullAlbum.Tracks.Items)
                {
                  if (!checkArtistThreshold || (checkArtistThreshold && await CheckArtistThreshold(item.Artists)))
                  {
                    if (albumTrack.Name.Contains("- Extended"))
                    {
                      continue;
                    }

                    TrackData spotifyTrack = new TrackData();
                    var fullAlbumTrack = await spotify.Tracks.Get(albumTrack.Id);

                    spotifyTrack.SpotifyArtists = albumTrack.Artists;
                    spotifyTrack.SpotifyExternalId = fullAlbumTrack.ExternalIds;
                    spotifyTrack.Title = albumTrack.Name;
                    spotifyTrack.Uri = albumTrack.Uri;
                    DateTime.TryParse(item.ReleaseDate, out DateTime releaseDate);
                    spotifyTrack.ReleaseDate = releaseDate;
                    spotifyTrackList.Add(spotifyTrack);
                    Console.WriteLine($"[SPOTIFY] Found track: [{spotifyTrack.Uri}] {spotifyTrack.SpotifyArtists.First().Name} - {spotifyTrack.Title}");
                  }
                }
              }
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error: {ex.Message}");
          }
        }
        else
        {
          try
          {
            searchRequest = new SearchRequest(SearchRequest.Types.Track, searchQuery);

            // Get track
            int count = 0;
            int nMaxRetries = 10;
            SearchResponse searchResult;
            while (true)
            {
              try
              {
                searchResult = await spotify.Search.Item(searchRequest);
                break;
              }
              catch (Exception ex)
              {
                if (++count == nMaxRetries)
                {
                  Console.WriteLine($"Error: {ex.Message}");
                }
              }
            }

            if (searchResult.Tracks.Items != null)
            {
              TrackData spotifyTrack = track;

              foreach (var item in searchResult.Tracks.Items)
              {
                if (chechForReleaseDate)
                {
                  // Add first track with todays release date
                  DateTime.TryParse(item.Album.ReleaseDate, out DateTime releaseDate);
                  if (await CheckReleaseDate(releaseDate))
                  {
                    if (!checkArtistThreshold || (checkArtistThreshold && await CheckArtistThreshold(item.Artists)))
                    {
                      spotifyTrack.SpotifyArtists = item.Artists;
                      spotifyTrack.SpotifyExternalId = item.ExternalIds;
                      spotifyTrack.Uri = item.Uri;
                      spotifyTrack.ReleaseDate = releaseDate;
                      spotifyTrackList.Add(spotifyTrack);
                    }
                    break;
                  }
                  else
                  {
                    continue;
                  }
                }
                else
                {
                  // Add first track
                  if (!checkArtistThreshold || (checkArtistThreshold && await CheckArtistThreshold(item.Artists)))
                  {
                    spotifyTrack.SpotifyArtists = item.Artists;
                    spotifyTrack.SpotifyExternalId = item.ExternalIds;
                    spotifyTrack.Uri = item.Uri;
                    DateTime.TryParse(item.Album.ReleaseDate, out DateTime releaseDate);
                    spotifyTrack.ReleaseDate = releaseDate;
                    spotifyTrackList.Add(spotifyTrack);
                  }
                  break;
                }
              }

              if (spotifyTrack.Uri != null)
              {
                Console.WriteLine($"[SPOTIFY] Found track: [{spotifyTrack.Uri}] {spotifyTrack.SpotifyArtists.First().Name} - {spotifyTrack.Title}");
              }
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"Error: {ex.Message}");
          }
        }
      }

      spotifyTrackList = spotifyTrackList.DistinctBy(track => track.Uri).ToList();
      return spotifyTrackList;
    }

    public async Task AddTracksToPlaylist(List<TrackData> trackList, string playlistId)
    {
      try
      {
        var playlistTracks = await spotify.Playlists.GetItems(playlistId);

        trackList.Reverse();
        Thread.Sleep(1000);

        foreach (var track in trackList)
        {
          if (track.SpotifyExternalId != null)
          {
            if (playlistTracks.Items.Any(item => (item.Track as FullTrack)?.ExternalIds.First().Value == track.SpotifyExternalId.First().Value))
            {
              //Track is already in playlist
              Console.WriteLine($"[SPOTIFY] Track '[{track.SpotifyExternalId.First().Value}] {track.SpotifyArtists.First().Name} - {track.Title}' already in playlist [{playlistId}]");
              continue;
            }
          }
          else if (track.Uri != null && (playlistTracks.Items.Any(item => (item.Track as FullTrack)?.Uri == track.Uri)))
          {
            //Track is already in playlist
            Console.WriteLine($"[SPOTIFY] Track '[{track.Uri}] {track.SpotifyArtists.First().Name} - {track.Title}' already in playlist [{playlistId}]");
            continue;
          }

          //Add track to playlist
          await spotify.Playlists.AddItems(playlistId, new PlaylistAddItemsRequest(new List<string> { track.Uri }));
          playlistTracks = await spotify.Playlists.GetItems(playlistId);

          Console.WriteLine($"[SPOTIFY] Added track '[{track.Uri}] {track.SpotifyArtists.First().Name} - {track.Title}' to playlist [{playlistId}]");

          //Needed for better order in playlist
          Thread.Sleep(1000);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error: {ex.Message}");
      }
    }

    public async Task RemoveOldTracksFromPlaylist(string playlistId)
    {
      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      int dateAddedThreshold = Int32.Parse(configurationBuilder.GetSection("Settings:DateAddedThreshold").Value);
      var dateThreshold = DateTime.UtcNow.AddDays(-1 * dateAddedThreshold).AddHours(1);

      try
      {
        var playlistTracks = await spotify.Playlists.GetItems(playlistId);

        List<PlaylistRemoveItemsRequest.Item> tracksToRemove = new List<Item>();

        var oldTracks = playlistTracks.Items
            .Where(item => item.AddedAt.HasValue && item.AddedAt.Value < dateThreshold)
            .Select(item => (item.Track as FullTrack)?.Uri)
            .ToList();

        foreach (var track in oldTracks)
        {
          tracksToRemove.Add(new Item { Uri = track });
        }

        if (tracksToRemove.Any())
        {
          var playlist = await spotify.Playlists.Get(playlistId);
          var snapshotId = playlist.SnapshotId;

          await spotify.Playlists.RemoveItems(
            playlistId,
            new PlaylistRemoveItemsRequest
            {
              Tracks = tracksToRemove,
              SnapshotId = snapshotId
            }
          );
          Console.WriteLine($"[SPOTIFY] Removed {tracksToRemove.Count} tracks from the playlist that were added > {dateAddedThreshold} days ago.");
        }
        else
        {
          Console.WriteLine("[SPOTIFY] No tracks to remove from the playlist.");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error: {ex.Message}");
      }
    }

    public List<string> GetArtistList(string artists)
    {
      //List of strings to split by
      String[] delimiters = { " & ", " and ", " featuring ", " features ", ",", " ft. ", " Ft. ", " feat. ", " Feat. ", " - " };

      List<string> artistList = artists.Split(delimiters, StringSplitOptions.None).ToList();

      return artistList;
    }

    public async Task<bool> CheckArtistThreshold(List<SimpleArtist> artists)
    {
      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      var thresholdFollowers = Int32.Parse(configurationBuilder.GetSection("Settings:ArtistThresholdFollowers").Value);
      var thresholdPopularity = Int32.Parse(configurationBuilder.GetSection("Settings:ArtistThresholdPopularity").Value);

      foreach (var artist in artists)
      {
        var fullArtist = await spotify.Artists.Get(artist.Id);
        var followers = fullArtist.Followers.Total;
        var popularity = fullArtist.Popularity;
        var name = fullArtist.Name;

        if (followers >= thresholdFollowers && popularity >= thresholdPopularity)
        {
          Console.WriteLine($"[SPOTIFY] Artist above threshold: F: {followers} [{thresholdFollowers}] P: {popularity} [{thresholdPopularity}] Artist: {fullArtist.Name}");
          return true;
        }
        else if (name.Equals("Various Artists"))
        {
          Console.WriteLine($"[SPOTIFY] Compilation album. Ignoring artist thresholds.");
          return true;
        }
        Console.WriteLine($"[SPOTIFY] Artist below threshold: F: {followers} [{thresholdFollowers}] P: {popularity} [{thresholdPopularity}] Artist: {fullArtist.Name}");
      }
      return false;
    }

    public async Task<bool> CheckReleaseDate(DateTime releaseDate)
    {
      DateTime currentDate = GetCurrentTimeWet();
      DateTime yesterdaysDate = currentDate.AddDays(-1);

      if(currentDate.Year == releaseDate.Year && currentDate.Month == releaseDate.Month && currentDate.Day == releaseDate.Day)
      {
        return true;
      } else if (yesterdaysDate.Year == releaseDate.Year && yesterdaysDate.Month == releaseDate.Month && yesterdaysDate.Day == releaseDate.Day)
      {
        Console.WriteLine("[App] Warning: Track was released yesterday.");
        return true;
      }

      return false;
    }

    public DateTime GetCurrentTimeWet()
    {
      TimeZoneInfo timeZoneWet = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
      DateTime timeNow = DateTime.UtcNow;
      return TimeZoneInfo.ConvertTimeFromUtc(timeNow, timeZoneWet);
    }
  }
}