using System.Collections.Generic;

namespace CustomHitSounds
{
    public class HitsoundConfig
    {
        public string name;
        public List<string> hit;
        public List<string> hit_hold;
        public List<string> hit_during_hold;
        public List<string> hold_loop;
        public List<string> dodge;
        public List<string> miss;
    }
}