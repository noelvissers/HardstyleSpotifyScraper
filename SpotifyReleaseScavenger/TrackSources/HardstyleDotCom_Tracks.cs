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
using System.Threading.Tasks;
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

      int trackPagesToCheck = Int32.Parse(configurationBuilder.GetSection("HardstyleReleaseRadarSettings:TrackPagesToCheck").Value);

      for (int i = 1; i <= trackPagesToCheck; i++)
      {
        Thread.Sleep(1000);
        HtmlDocument doc = web.Load(@"https://music.hardstyle.com/hardstyle-releases/tracks/page/" + i.ToString());
        IEnumerable<HtmlNode> nodeTrackTracks = doc.DocumentNode
          .Descendants("td")
          .Where(n => n.HasClass("text-1"));

        var nodeTracks = nodeTrackTracks.Select(item => item.FirstChild).ToList();

        foreach (var nodeTrack in nodeTracks)
        {
          TrackData track = new TrackData();
          int nodeElement = 0;

          foreach (var nodeTrackChild in nodeTrack.ChildNodes)
          {
            nodeElement++;
            if (nodeTrackChild.Name == "b" && nodeElement % 2 == 1)
            {
              track.Title = nodeTrackChild.InnerHtml;
            }
            else if (nodeTrackChild.Name == "span")
            {
              track.Artist = nodeTrackChild.InnerHtml;
            }
          }
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
