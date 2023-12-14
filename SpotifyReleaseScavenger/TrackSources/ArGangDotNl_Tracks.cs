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
  internal class ArGangDotNl_Tracks : IScavenge
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

      // Check tracks
      HtmlDocument doc = web.Load(url);
      IEnumerable<HtmlNode> nodeTracks = doc.DocumentNode
        .Descendants("li")
        .Where(n => n.GetAttributeValue("class", "").Contains("product_cat-tracks"));

      int tracksToCheck = Int32.Parse(configurationBuilder.GetSection("ArGangDotNlSettings:TracksToCheck").Value);
      int tracksChecked = 0;
      foreach (var node in nodeTracks)
      {
        if (!node.GetAttributeValue("class", "").Contains("product_cat-albums"))
        {
          if (tracksChecked == tracksToCheck)
          {
            break;
          }

          TrackData track = new TrackData();

          string trackLink = GetStringBetween(node.InnerHtml, "href=\"", "\"");
          HtmlDocument trackDoc = web.Load(trackLink);
          IEnumerable<HtmlNode> nodeTrack = trackDoc.DocumentNode
            .Descendants("div")
            .Where(n => n.GetAttributeValue("class", "").Contains("woocommerce-product-details__short-description"));

          // Artist
          if (nodeTrack.First().Element("p").Element("a") != null)
          {
            string trackArtist = HttpUtility.HtmlDecode(nodeTrack.First().Element("p").Element("a").InnerHtml);
            trackArtist = trackArtist.Split(new string[] { " – " }, StringSplitOptions.RemoveEmptyEntries)[0];
            track.Artist = trackArtist.Trim();
          }
          else if (nodeTrack.First().Element("p") != null)
          {
            string trackArtist = HttpUtility.HtmlDecode(nodeTrack.First().Element("p").InnerHtml);
            trackArtist = trackArtist.Split(new string[] { " – " }, StringSplitOptions.RemoveEmptyEntries)[0];
            track.Artist = trackArtist.Trim();
          }

          // Title
          string trackTitle = HttpUtility.HtmlDecode(node.InnerText);
          trackTitle = trackTitle.Split(new string[] { "(Original Mix)" }, StringSplitOptions.RemoveEmptyEntries)[0];
          trackTitle = trackTitle.Split(new string[] { "(Extended Mix)" }, StringSplitOptions.RemoveEmptyEntries)[0];
          track.Title = trackTitle.Trim();

          track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{track.Artist}{track.Title}")));

          Console.WriteLine($"[argang.nl | Tracks] [{track.Hash}] {track.Artist} - {track.Title}");
          trackData.Add(track);

          tracksChecked++;
        }
      }

      Console.WriteLine($"[argang.nl | Tracks] Retrieved a total of {trackData.Count()} entries.");
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
