using SpotifyReleaseScavenger.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyReleaseScavenger.Models
{
  public class PlaylistData
  {
    public string? Id { get; set; }
    public List<IScavenge>? Sources { get; set; }
    public PlaylistData(string id, List<IScavenge>? sources)
    {
      Id = id;
      Sources = sources;
    }
  }
}
