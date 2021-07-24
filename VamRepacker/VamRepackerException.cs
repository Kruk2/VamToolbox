using System;
using System.Runtime.Serialization;

namespace VamRepacker
{
    [Serializable]
    internal class VamRepackerException : Exception
    {
        public VamRepackerException()
        {
        }

        public VamRepackerException(string message)
            : base(message)
        {
        }

        public VamRepackerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected VamRepackerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
