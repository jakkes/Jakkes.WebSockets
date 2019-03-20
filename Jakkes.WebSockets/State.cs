using System;

namespace Jakkes.WebSockets
{
    public enum State
    {
        Open, Closed, Closing
    }

    public class StateChangedEventArgs : EventArgs {
        private readonly State _state;
        
        public State State => _state;
        public StateChangedEventArgs(State state)
        {
            _state = state;
        }
    }
}