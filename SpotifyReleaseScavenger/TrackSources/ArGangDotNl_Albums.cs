using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using SpotifyReleaseScavenger.Interfaces;
using SpotifyReleaseScavenger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SpotifyReleaseScavenger.TrackSources
{
  internal class ArGangDotNl_Albums : IScavenge
  {
    public List<TrackData> GetTracks()
    {
      List<TrackData> trackData = new List<TrackData>();
      HtmlWeb web = new HtmlWeb();
      string url = "https://argang.nl/product-category/tracks/?orderby=date&wpf_filter_cat_1=17&wpf_count=1000&wpf_fbv=1";

      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      // Check albums
      HtmlDocument doc = web.Load(url);
      IEnumerable<HtmlNode> nodeAlbums = doc.DocumentNode
        .Descendants("li")
        .Where(n => n.GetAttributeValue("class", "").Contains("product_cat-albums"));

      int albumsToCheck = Int32.Parse(configurationBuilder.GetSection("ArGangDotNlSettings:AlbumsToCheck").Value);
      int albumsChecked = 0;
      foreach (var node in nodeAlbums)
      {
        if (albumsChecked == albumsToCheck)
        {
          break;
        }

        TrackData track = new TrackData();

        string albumLink = GetStringBetween(node.InnerHtml, "href=\"", "\"");
        HtmlDocument albumDoc = web.Load(albumLink);
        IEnumerable<HtmlNode> nodeTrack = albumDoc.DocumentNode
          .Descendants("div")
          .Where(n => n.GetAttributeValue("class", "").Contains("woocommerce-product-details__short-description"));

        // Artist
        if (nodeTrack.First().Element("p").Element("a") != null)
        {
          string albumArtist = HttpUtility.HtmlDecode(nodeTrack.First().Element("p").Element("a").InnerHtml).Trim();
          albumArtist = albumArtist.Split(new string[] { " – " }, StringSplitOptions.RemoveEmptyEntries)[0];
          track.Artist = albumArtist;
        }
        else if (nodeTrack.First().Element("p") != null)
        {
          string albumArtist = HttpUtility.HtmlDecode(nodeTrack.First().Element("p").InnerHtml).Trim();
          albumArtist = albumArtist.Split(new string[] { " – " }, StringSplitOptions.RemoveEmptyEntries)[0];
          track.Artist = albumArtist.Trim();
        }

        // Title
        string albumTitle = HttpUtility.HtmlDecode(node.InnerText);
        albumTitle = albumTitle.Split(new string[] { "- Mini Album" }, StringSplitOptions.RemoveEmptyEntries)[0];
        albumTitle = albumTitle.Split(new string[] { "- EP" }, StringSplitOptions.RemoveEmptyEntries)[0];
        albumTitle = albumTitle.Split(new string[] { "(Digital Album)" }, StringSplitOptions.RemoveEmptyEntries)[0];
        albumTitle = albumTitle.Split(new string[] { "(Album)" }, StringSplitOptions.RemoveEmptyEntries)[0];
        track.Title = albumTitle.Trim();

        // Album
        track.IsAlbum = true;

        track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"ALBUM{track.Artist}{track.Title}")));

        Console.WriteLine($"[argang.nl | Albums] [{track.Hash}] {track.Artist} - {track.Title}");
        trackData.Add(track);

        albumsChecked++;
      }

      Console.WriteLine($"[argang.nl | Albums] Retrieved a total of {trackData.Count()} entries.");
      return trackData;
    }
    public string GetStringBetween(string text, string firstString, string lastString)
    {
      int pos1 = text.IndexOf(firstString) + firstString.Length;
      int pos2 = text.IndexOf(lastString, pos1);

      return text.Substring(pos1, pos2 - pos1);
    }
  }
}
