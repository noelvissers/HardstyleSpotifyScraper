using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using SpotifyAPI.Web;
using SpotifyReleaseScavenger.Interfaces;
using SpotifyReleaseScavenger.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace SpotifyReleaseScavenger.TrackSources
{
  public class HardstyleDotCom_Albums : IScavenge
  {
    public List<TrackData> GetTracks()
    {
      List<TrackData> trackData = new List<TrackData>();
      HtmlWeb web = new HtmlWeb();
      string url = "https://hardstyle.com/en/albums?genre=Hardcore%7EHardstyle%7EUptempo";

      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      // Check albums
      HtmlDocument doc = web.Load(url);
      IEnumerable<HtmlNode> nodeAlbums = doc.DocumentNode
        .Descendants("div")
        .Where(n => n.HasClass("trackContent"));

      int albumsToCheck = Int32.Parse(configurationBuilder.GetSection("HardstyleDotComSettings:AlbumsToCheck").Value);
      int albumsChecked = 0;
      foreach (var node in nodeAlbums)
      {
        if (albumsChecked >= albumsToCheck)
        {
          break;
        }

        TrackData track = new TrackData();

        track.Title = Regex.Replace(HttpUtility.HtmlDecode(node.ChildNodes.ElementAt(0).InnerText), @"\s+", " ").Replace("\n", "").Trim();
        track.Artist = Regex.Replace(HttpUtility.HtmlDecode(node.ChildNodes.ElementAt(2).InnerText), @"\s+", " ").Replace("\n", "").Trim();
        track.IsAlbum = true;

        track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"ALBUM{track.Artist}{track.Title}")));

        Console.WriteLine($"[Hardstyle.com | Albums] [{track.Hash}] {track.Artist} - {track.Title}");
        trackData.Add(track);

        albumsChecked++;
      }

      Console.WriteLine($"[Hardstyle.com | Albums] Retrieved a total of {trackData.Count()} entries.");
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
