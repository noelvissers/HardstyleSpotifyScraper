using SpotifyReleaseScavenger.Models;

namespace SpotifyReleaseScavenger.Interfaces
{
  public interface IScavenge
  {
    public List<TrackData> GetTracks();
  }
}
