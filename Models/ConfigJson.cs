using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCProjectBot.Models
{
    public class ConfigJson
    {
        public ulong RequestsChannelId { get; set; }

        public ulong ProjectsCategoryId { get; set; }

        // public ulong RequestsGuildId { get; set; }

        public ulong FallbackNotifyChannel { get; set; }
    }
}
