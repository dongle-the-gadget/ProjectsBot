using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCProjectBot.Models
{
    public class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; }

        public string Description { get; set; }

        public ulong Registrant { get; set; }

        public string? Download { get; set; }

        public ulong AssociatedChannelId { get; set; }

        public ulong AssociatedGuild { get; set; }

        public Dictionary<ProjectRole, Dictionary<ulong, ulong>> AssociatedDiscordRoles { get; set; } = new();

        public Dictionary<ulong, ProjectRole> UserRoles { get; set; } = new();
    }
}
