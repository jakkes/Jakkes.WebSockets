using System;
using System.Collections.Generic;
using System.Text;
using Jakkes.WebSockets;

namespace Jakkes.WebSockets
{
    public class Message
    {
        internal OpCode opCode { get; private set; }
        public MessageType Type { get; private set; }
        public byte[] Data { get; private set; }
        public Action OnSuccess { get; private set; }
        public Action OnFail { get; private set;}

        /// <summary>
        /// Gets the text contained in the message. Throws if Type is not set to Text.
        /// </summary>
        /// <exception cref="InvalidTypeException">When Type is not set to Text</exception>
        public string Text
        {
            get
            {
                if (Type != MessageType.Text)
                    throw new InvalidTypeException();
                return Encoding.UTF8.GetString(Data);
            }
        }

        public Message(string message) : this(message, null, null) {}
        public Message(string message, Action onSuccess, Action onFail){
            Data = Encoding.UTF8.GetBytes(message);
            opCode = OpCode.TextFrame;
            Type = MessageType.Text;

            OnSuccess = onSuccess;
            OnFail = onFail;
        }
        public Message(byte[] binary) : this(binary, null, null) { }
        public Message(byte[] binary, Action onSuccess, Action onFail)
        {
            Data = binary;
            opCode = OpCode.BinaryFrame;
            Type = MessageType.Binary;

            OnSuccess = onSuccess;
            OnFail = onFail;
        }
        internal Message(byte[] data, OpCode opcode, Action onSuccess, Action onFail) {
            Data = data;
            opCode = opcode;
            OnSuccess = onSuccess;
            OnFail = onFail;
        }
        internal Message(byte[] data, OpCode opcode) : this(data, opcode, null, null) { }

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
