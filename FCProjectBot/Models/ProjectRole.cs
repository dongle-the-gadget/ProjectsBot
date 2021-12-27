using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FCProjectBot.Models
{
    [Flags]
    public enum ProjectRole
    {
        [ChoiceName("Leader")]
        LeadDev = 1,

        [ChoiceName("Bug Reporter")]
        BugReport = 2,

        [ChoiceName("Sponsor/Donator")]
        Sponsor = 4,

        [ChoiceName("Researcher")]
        Researcher = 8,

        [ChoiceName("Translator")]
        Translator = 16,

        [ChoiceName("Designer")]
        Designer = 32,

        [ChoiceName("Developer")]
        Dev = 64,

        [ChoiceName("Beta Tester")]
        BetaTester = 128
    } 
}
