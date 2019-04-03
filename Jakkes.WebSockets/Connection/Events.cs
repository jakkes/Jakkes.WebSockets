using System;

namespace Jakkes.WebSockets {
    public partial class Connection {

        public event EventHandler<TextReceivedEventArgs> TextReceived;
        public event EventHandler<BinaryReceivedEventArgs> BinaryReceived;
        public event EventHandler<MessageSentEventArgs> MessageSent;
        public event EventHandler<StateChangedEventArgs> StateChanged;
    }
    public class TextReceivedEventArgs : EventArgs
    {
        private readonly string _text;
        
        public string Text => _text;
        public TextReceivedEventArgs(string text)
        {
            _text = text;
        }
    }
    public class BinaryReceivedEventArgs : EventArgs {
        private readonly byte[] _binary;
        
        public byte[] Binary => _binary;
        public BinaryReceivedEventArgs(byte[] binary)
        {
            _binary = binary;
        }
    }
    public class MessageSentEventArgs : EventArgs {
        private readonly Message _message;
        
        public Message Message => _message;
        public MessageSentEventArgs(Message msg)
        {
            _message = msg;
        }
    }

    
}