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
using System.Threading.Tasks;
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

      HtmlDocument doc = web.Load(@"https://music.hardstyle.com/hardstyle-releases/albums");
      IEnumerable<HtmlNode> nodeAlbums = doc.DocumentNode
        .Descendants("td")
        .Where(n => n.HasClass("text-1"));

      var configurationBuilder = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddUserSecrets<Program>()
        .Build();

      int albumsToCheck = Int32.Parse(configurationBuilder.GetSection("HardstyleReleaseRadarSettings:AlbumsToCheck").Value);
      int albumsChecked = 0;

      foreach (var node in nodeAlbums)
      {
        if (albumsChecked >= albumsToCheck)
        {
          break;
        }
        albumsChecked++;

        string albumData = GetStringBetween(node.InnerHtml, "href=\"", "\">");

        Thread.Sleep(1000);
        HtmlDocument albumDetail = web.Load(albumData);

        IEnumerable<HtmlNode> nodeAlbum = albumDetail.DocumentNode
          .Descendants("td")
          .Where(n => n.HasClass("text-1"));

        var nodeAlbumTracks = nodeAlbum.Select(item => item.FirstChild).ToList();

        long indexStart = 0;
        foreach (var nodeAlbumTrack in nodeAlbumTracks)
        {
          string compare = GetStringBetween(nodeAlbumTrack.OuterHtml, "href=\"", "\"><b class");
          int indexCompare = compare.LastIndexOf('/');
          compare = compare.Substring(indexCompare + 1, compare.Length - indexCompare - 1);
          long indexCurrent = long.Parse(compare);

          if (indexStart == 0)
          {
            indexStart = indexCurrent;
          }
          else if (indexStart != indexCurrent)
          {
            continue;
          }
          indexStart++;

          TrackData track = new TrackData();
          int nodeElement = 0;

          foreach (var nodeAlbumTrackChild in nodeAlbumTrack.ChildNodes)
          {
            nodeElement++;
            if (nodeAlbumTrackChild.Name == "b" && nodeElement % 2 == 1)
            {
              track.Title = nodeAlbumTrackChild.InnerHtml;
            }
            else if (nodeAlbumTrackChild.Name == "span")
            {
              track.Artist = nodeAlbumTrackChild.InnerHtml;
            }
          }
          track.Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{track.Artist}{track.Title}")));

          Console.WriteLine($"[Hardstyle.com | Albums] [{track.Hash}] {track.Artist} - {track.Title}");
          trackData.Add(track);
        }
      }

      Console.WriteLine($"[Hardstyle.com | Albums] Retrieved a total of {trackData.Count()} entries.");
      return trackData;
    }

    public string GetStringBetween(string text, string firstString, string lastString)
    {
      int pos1 = text.IndexOf(firstString) + firstString.Length;
      int pos2 = text.IndexOf(lastString);

      return text.Substring(pos1, pos2 - pos1);
    }
  }
}
