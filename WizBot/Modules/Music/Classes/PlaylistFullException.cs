﻿using System;

namespace WizBot.Modules.Music.Classes
{
    class PlaylistFullException : Exception
    {
        public PlaylistFullException(string message) : base(message)
        {
        }
        public PlaylistFullException() : base("Queue is full.") { }
    }
}
