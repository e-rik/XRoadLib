﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using XRoadLib.Extensions;
using XRoadLib.Protocols;
using XRoadLib.Protocols.Headers;
using XRoadLib.Serialization;
using XRoadLib.Serialization.Mapping;

namespace XRoadLib
{
    public interface IXRoadRequest
    {
        TResult Execute<TResult>(object arg, IXRoadHeader xRoadHeader, IServiceMap serviceMap = null);
    }

    public class XRoadRequest : IXRoadRequest
    {
        private readonly XRoadProtocol protocol;
        private readonly Uri uri;
        private readonly string requestNamespace;

        public XRoadRequest(Uri uri, XRoadProtocol protocol, string requestNamespace)
        {
            this.protocol = protocol;
            this.uri = uri;
            this.requestNamespace = requestNamespace;
        }

        public T Execute<T>(object arg, IXRoadHeader xRoadHeader, IServiceMap serviceMap = null)
        {
            using (var requestMessage = new XRoadMessage(protocol, xRoadHeader))
            {
                using (var writer = XmlWriter.Create(requestMessage.ContentStream))
                {
                    writer.WriteStartDocument();

                    protocol.WriteSoapEnvelope(writer);
                    if (!string.IsNullOrEmpty(requestNamespace))
                        writer.WriteAttributeString(PrefixConstants.XMLNS, "req", NamespaceConstants.XMLNS, requestNamespace);

                    protocol.WriteSoapHeader(writer, xRoadHeader);

                    writer.WriteStartElement("Body", NamespaceConstants.SOAP_ENV);

                    serviceMap = serviceMap ?? requestMessage.GetSerializerCache().GetServiceMap(XName.Get(xRoadHeader.Service.ServiceCode, protocol.ProducerNamespace));
                    serviceMap.SerializeRequest(writer, arg, requestMessage, requestNamespace);

                    writer.WriteEndElement();
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                }

                var request = WebRequest.Create(uri);

                request.ContentType = $"text/xml; charset={XRoadEncoding.UTF8.WebName}";
                request.Headers["SOAPAction"] = string.Empty;
                request.Method = "POST";

                requestMessage.SaveTo(request);

#if NET40
                using (var response = request.GetResponse())
#else
                using (var response = request.GetResponseAsync().Result)
#endif
                using (var responseStream = response.GetResponseStream())
                using (var seekableStream = new MemoryStream())
                using (var responseMessage = new XRoadMessage())
                {
                    responseStream?.CopyTo(seekableStream);
                    responseMessage.LoadResponse(seekableStream, response.Headers.GetContentTypeHeader(), Path.GetTempPath(), Enumerable.Repeat(protocol, 1));
                    return (T)responseMessage.DeserializeMessageContent(xRoadHeader.Service.ServiceCode);
                }
            }
        }
    }
}