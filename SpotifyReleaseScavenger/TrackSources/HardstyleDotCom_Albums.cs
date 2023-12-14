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

      int albumsToCheck = Int32.Parse(configurationBuilder.GetSection("HardstyleDotComSettings:AlbumsToCheck").Value);
      int albumsChecked = 0;

      foreach (var node in nodeAlbums)
      {
        if (albumsChecked >= albumsToCheck)
        {
          break;
        }

        // TODO: Check albums

        // Make sure these tracks arent added twice: https://open.spotify.com/album/5i6jgMm2JAYNLSN6YgBHSF?si=yjjOyMpSStqARs-t5zX3TQ
        // Only normal track is fine.
        // This should be in tracks to add, but noted here

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
