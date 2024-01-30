using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyReleaseScavenger.Interfaces;
using SpotifyReleaseScavenger.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace SpotifyReleaseScavenger.TrackSources
{
  public class HardstyleDotCom_Tracks : IScavenge
  {
    public List<TrackData> GetTracks()
    {
      List<TrackData> trackData = new List<TrackData>();
      HtmlWeb web = new HtmlWeb();

      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      int trackPagesToCheck = Int32.Parse(configurationBuilder.GetSection("HardstyleDotComSettings:TrackPagesToCheck").Value);

      for (int i = 1; i <= trackPagesToCheck; i++)
      {
        HtmlDocument doc = web.Load(@"https://hardstyle.com/en/tracks?page=" + i.ToString() + "&genre=Hardcore~Hardstyle~Uptempo");
        IEnumerable<HtmlNode> nodeTracks = doc.DocumentNode
          .Descendants("div")
          .Where(n => n.HasClass("trackContent"));

        foreach (var node in nodeTracks)
        {
          TrackData track = new TrackData();

          track.Title = Regex.Replace(HttpUtility.HtmlDecode(node.ChildNodes.ElementAt(0).InnerText), @"\s+", " ").Replace("\n", "").Trim();

          if (node.ChildNodes.ElementAt(1).InnerText.ToLower().Contains("remix"))
          {
            track.Title += $" {Regex.Replace(HttpUtility.HtmlDecode(node.ChildNodes.ElementAt(1).InnerText), @"\s+", " ").Replace("\n", "").Trim()}";
          }

          track.Artist = Regex.Replace(HttpUtility.HtmlDecode(node.ChildNodes.ElementAt(2).InnerText), @"\s+", " ").Replace("\n", "").Trim();

          track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{track.Artist}{track.Title}")));

          Console.WriteLine($"[Hardstyle.com | Tracks] [{track.Hash}] {track.Artist} - {track.Title}");
          trackData.Add(track);
        }
      }

      Console.WriteLine($"[Hardstyle.com | Tracks] Retrieved a total of {trackData.Count()} entries.");
      return trackData;
    }
  }
}
