using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using Netina.Stomp.Client.Utils;

namespace Netina.Stomp.Client.Messages
{
    public class StompBinaryMessageSerializer
    {
        public byte[] Serialize(StompMessage message)
        {
            var resultBuffer = new List<byte>();
            resultBuffer.AddRange(Encoding.UTF8.GetBytes($"{message.Command}\n"));

            if (message.Headers?.Count > 0)
            {
                foreach (var messageHeader in message.Headers)
                {
                    resultBuffer.AddRange(Encoding.UTF8.GetBytes($"{messageHeader.Key}:{messageHeader.Value}\n"));
                }
            }
            resultBuffer.Add((byte)'\n');
            resultBuffer.AddRange(message.BinaryBody);
            resultBuffer.Add((byte)'\0');

            return resultBuffer.ToArray();
        }

        public StompMessage Deserialize(byte[] message)
        {
            var headerBuffer = new List<byte>();
            var bodyBuffer = new List<byte>();
            byte previousByte = 0;
            var isBodyStarted = false;
            foreach (var currentByte in message)
            {
                if (currentByte == previousByte && previousByte == (byte)'\n')
                {
                    isBodyStarted = true;
                }
                else
                {
                    if (isBodyStarted)
                    {
                        bodyBuffer.Add(currentByte);
                    }
                    else
                    {
                        headerBuffer.Add(currentByte);
                    }
                }

                previousByte = currentByte;
            }

            var command = string.Empty;
            var headers = new Dictionary<string, string>();

            if (headerBuffer.Count > 0)
            {
                var stringHeader = Encoding.UTF8.GetString(headerBuffer.ToArray());

                using (var reader = new StringReader(stringHeader))
                {
                    command = reader.ReadLine();
                    var header = reader.ReadLine();
                    while (!string.IsNullOrEmpty(header))
                    {
                        var separatorIndex = header.IndexOf(':');
                        if (separatorIndex != -1)
                        {
                            var name = header.Substring(0, separatorIndex);
                            var value = header.Substring(separatorIndex + 1);
                            headers[name] = value;
                        }

                        header = reader.ReadLine() ?? string.Empty;
                    }
                }
            }

            if (headers.TryGetValue(StompHeader.ContentLength, out var contentLength))
            {
                if (long.TryParse(contentLength, out var length))
                {
                    // -2 is here to compensate for message termination characters in the end of the byte array - [\0\10] 
                    if (length != bodyBuffer.Count - 2)
                    {
                        throw new ApplicationException(
                            "STOMP: Content length header value is different then actual length of bytes received.");
                    }
                }
            }


            return new StompMessage(command, bodyBuffer.ToArray(), headers);
        }
    }
}