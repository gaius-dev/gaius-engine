using System;

namespace Gaius.Worker.Models
{
    public class GenerationInfo
    {
        public string GaiusVersion { get; internal set; }
        public DateTime GenerationDateTime { get; internal set;}
    }
}