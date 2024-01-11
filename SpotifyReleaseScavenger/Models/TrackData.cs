using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyReleaseScavenger.Models
{
  public class TrackData
  {
    public string Title { get; set; }
    public string Artist { get; set; }
    public bool IsAlbum { get; set; }
    public List<SimpleArtist> SpotifyArtists { get; set; }
    public Dictionary<string, string> SpotifyExternalId { get; set; }
    public string Hash { get; set; }
    public string Uri { get; set; }
    public DateTime ReleaseDate { get; set; }
  }
}
