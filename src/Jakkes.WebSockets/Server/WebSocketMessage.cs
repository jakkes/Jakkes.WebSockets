using System;
using System.Text;

namespace Jakkes.WebSockets.Server
{

    public class Message{
        internal OpCode opCode { get; set; }
        public MessageType Type { get; private set; }
        public byte[] Data { get; internal set; }
        public Action OnComplete { get; set; }
        internal Message(){}
        public Message(byte[] binary){
            Data = binary;
            opCode = OpCode.BinaryFrame;
            Type = MessageType.Binary;
        }
        public Message(string msg){
            Data = Encoding.UTF8.GetBytes(msg);
            opCode = OpCode.TextFrame;
            Type = MessageType.Text;
        }
        public Message(string msg, Action onComplete) : this(msg){
            OnComplete = onComplete;
        }
        public Message(byte[] binary, Action onComplete) : this(binary){
            OnComplete = onComplete;
        }
        internal Message(byte[] data, OpCode opCode){
            this.opCode = opCode;
            Data = data;
        }
        internal Message(byte[] data, OpCode opCode, Action onComplete) : this(data, opCode){
            OnComplete = onComplete;
        }
    }
    public enum MessageType{
        Text,
        Binary
    }
}