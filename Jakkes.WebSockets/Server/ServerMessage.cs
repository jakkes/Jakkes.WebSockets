using System;
using System.Collections.Generic;
using System.Text;

namespace Jakkes.WebSockets.Server
{

    
    
    public class ServerMessage
    {
        internal OpCode opCode { get; private set; }
        public MessageType Type { get; private set; }
        public byte[] Data { get; private set; }

        /// <summary>
        /// Gets the text contained in the message. Throws if Type is not set to Text.
        /// </summary>
        /// <exception cref="InvalidTypeException">When Type is not set to Text</exception>
        public string Message
        {
            get
            {
                if (Type != MessageType.Text)
                    throw new InvalidTypeException();
                return Encoding.UTF8.GetString(Data);
            }
        }
        
        public ServerMessage(string message)
        {
            Data = Encoding.UTF8.GetBytes(message);
            opCode = OpCode.TextFrame;
            Type = MessageType.Text;
        }
        public ServerMessage(byte[] binary)
        {
            Data = binary;
            opCode = OpCode.BinaryFrame;
        }
        internal ServerMessage(byte[] data, OpCode opcode)
        {
            Data = data;
            opCode = opcode;
        }

        public enum MessageType
        {
            Text,
            Binary
        }

        public class InvalidTypeException : Exception
        {

        }
    }
}
