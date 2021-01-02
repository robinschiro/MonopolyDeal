using System.Collections.Generic;

namespace GameObjects
{
    public class PlaySoundRequest
    {
        public string UriPath { get; set; }

        public IList<string> NamesOfPlayersToExclude { get; set; }

        public PlaySoundRequest( string uriPath, IList<string> namesOfPlayersToExclude = null )
        {
            this.UriPath = uriPath;
            this.NamesOfPlayersToExclude = namesOfPlayersToExclude ?? new List<string>();
        }
    }
}
