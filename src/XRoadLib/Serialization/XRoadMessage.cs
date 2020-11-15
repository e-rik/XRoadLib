﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using XRoadLib.Schema;
using XRoadLib.Serialization.Mapping;
using XRoadLib.Serialization.Template;
using XRoadLib.Headers;
using XRoadLib.Soap;
using XRoadLib.Styles;

namespace XRoadLib.Serialization
{
    /// <summary>
    /// Deserialized content of X-Road message (input or output).
    /// </summary>
    public class XRoadMessage : IAttachmentManager
    {
        public IServiceManager ServiceManager { get; internal set; }

        private readonly List<XRoadAttachment> _attachments = new List<XRoadAttachment>();

        /// <summary>
        /// When enabled, serialization/deserialization process applies filter specified in
        /// <see>FilterName</see> field, to exclude certain elements from outgoing messages, or ignore
        /// their values in incoming messages.
        /// </summary>
        public bool EnableFiltering { get; private set; }

        /// <summary>
        /// Specifies filter which is used on this particular X-Road message, when
        /// <see>EnableFiltering</see> is set to <value>true</value>.
        /// </summary>
        public string FilterName { get; private set; }

        /// <summary>
        /// Allows to limit depth of serialization/deserialization for recursive type
        /// definitions.
        /// Template allows to skip serialization of certain XML elements of the X-Road
        /// message (elements are present in document, but set to null even if type contains value).
        /// For deserialization incoming elements are ignored even if they contain value.
        /// </summary>
        public IXmlTemplate XmlTemplate { get; set; }

        /// <summary>
        /// Content type of the MIME multipart message container.
        /// </summary>
        public string MultipartContentType { get; set; }

        /// <summary>
        /// Encoding that is used to transfer X-Road message content.
        /// </summary>
        public Encoding ContentEncoding { get; set; } = XRoadEncoding.Utf8;

        /// <summary>
        /// XML document part of the X-Road message.
        /// </summary>
        public Stream ContentStream { get; set; }

        /// <summary>
        /// X-Road message style used to serialize/deserialize this message.
        /// </summary>
        public Style Style => ServiceManager.Style;

        /// <summary>
        /// X-Road protocol compliant header values extracted from SOAP header of
        /// the message.
        /// </summary>
        public ISoapHeader Header { get; set; }

        /// <summary>
        /// Remaining non-standard elements in X-Road message header.
        /// </summary>
        public IList<XElement> UnresolvedHeaders { get; set; }

        /// <summary>
        /// Identifies operation name for the X-Road message.
        /// </summary>
        public XName RootElementName { get; set; }

        /// <summary>
        /// Serialization format for binary content.
        /// </summary>
        public BinaryMode BinaryMode { get; set; }

        /// <summary>
        /// Shows if this X-Road message wrapped inside MIME multipart container.
        /// </summary>
        public bool IsMultipartContainer { get; set; }

        /// <summary>
        /// When X-Road message represents X-Road meta service operation, this
        /// property is assigned to correct service map to handle the operation.
        /// </summary>
        public IServiceMap MetaServiceMap { get; set; }

        /// <summary>
        /// Entire length of the X-Road message in serialized form.
        /// </summary>
        public long ContentLength { get; internal set; }

        /// <summary>
        /// All attachments (including inline content) that are packaged together
        /// with current X-Road message.
        /// </summary>
        public IList<XRoadAttachment> AllAttachments => _attachments;

        /// <summary>
        /// Multipart attachments that are packaged together with current X-Road message.
        /// </summary>
        public IEnumerable<XRoadAttachment> MultipartContentAttachments { get { return _attachments.Where(x => x.IsMultipartContent); } }

        /// <summary>
        /// Operation version of current X-Road message.
        /// </summary>
        public uint Version => (Header as IXRoadHeader)?.Service?.Version ?? 0u;

        /// <summary>
        /// X-Road message template request element root node.
        /// </summary>
        public IXmlTemplateNode RequestNode => XmlTemplate != null ? XmlTemplate.RequestNode : XRoadXmlTemplate.EmptyNode;

        /// <summary>
        /// X-Road message template response element root node.
        /// </summary>
        public IXmlTemplateNode ResponseNode => XmlTemplate != null ? XmlTemplate.ResponseNode : XRoadXmlTemplate.EmptyNode;

        /// <summary>
        /// Initializes new empty X-Road message for deserialization.
        /// </summary>
        public XRoadMessage()
        { }

        /// <summary>
        /// Initializes new empty X-Road message for request serialization.
        /// </summary>
        public XRoadMessage(IServiceManager serviceManager, ISoapHeader header)
            : this(new MemoryStream())
        {
            ServiceManager = serviceManager;
            Header = header;
        }

        /// <summary>
        /// Initializes new empty X-Road message for response serialization.
        /// </summary>
        public XRoadMessage(Stream contentStream)
        {
            ContentStream = contentStream;
        }

        /// <summary>
        /// Find X-Road message attachment by content ID.
        /// </summary>
        public XRoadAttachment GetAttachment(string contentId)
        {
            return _attachments.FirstOrDefault(attachment => attachment.ContentId.Contains(contentId));
        }

        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(Stream stream, IMessageFormatter messageFormatter, string contentTypeHeader, string storagePath, IServiceManager serviceManager)
        {
            ServiceManager = serviceManager;
            LoadRequest(stream, messageFormatter, contentTypeHeader, storagePath, Enumerable.Empty<IServiceManager>());
        }

        /// <summary>
        /// Loads X-Road message contents from request message.
        /// </summary>
        public void LoadRequest(Stream stream, IMessageFormatter messageFormatter, string contentTypeHeader, string storagePath, IEnumerable<IServiceManager> serviceManagers)
        {
            using (var reader = new XRoadMessageReader(stream, messageFormatter, contentTypeHeader, storagePath, serviceManagers))
                reader.Read(this);
        }

        /// <summary>
        /// Loads X-Road message contents from response message.
        /// </summary>
        public void LoadResponse(Stream stream, IMessageFormatter messageFormatter, string contentTypeHeader, string storagePath, IServiceManager serviceManager)
        {
            LoadResponse(stream, messageFormatter, contentTypeHeader, storagePath, new [] { serviceManager });
        }

        /// <summary>
        /// Loads X-Road message contents from response message.
        /// </summary>
        public void LoadResponse(Stream stream, IMessageFormatter messageFormatter, string contentTypeHeader, string storagePath, IEnumerable<IServiceManager> serviceManagers)
        {
            using (var reader = new XRoadMessageReader(stream, messageFormatter, contentTypeHeader, storagePath, serviceManagers))
                reader.Read(this, true);
        }

        /// <summary>
        /// Serializes X-Road message into specified web request.
        /// </summary>
        public void SaveTo(WebRequest webRequest, IMessageFormatter messageFormatter)
        {
            using (var outputStream = webRequest.GetRequestStreamAsync().Result)
            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, contentType => webRequest.ContentType = contentType, (k, v) => webRequest.Headers[k] = v, messageFormatter);
        }

        /// <summary>
        /// Serializes X-Road message into specified stream.
        /// </summary>
        public void SaveTo(Stream outputStream, Action<string> setContentType, Action<string, string> appendHeader, IMessageFormatter messageFormatter)
        {
            using (var writer = new XRoadMessageWriter(outputStream))
                writer.Write(this, setContentType, appendHeader, messageFormatter);
        }

        /// <summary>
        /// Clean up unmanaged resources allocated by the X-Road message.
        /// </summary>
        public void Dispose()
        {
            if (ContentStream != null)
            {
                ContentStream.Dispose();
                ContentStream = null;
            }

            foreach (var attachment in _attachments)
                attachment.Dispose();

            _attachments.Clear();
        }

        /// <summary>
        /// Copy X-Road message parts from another message, which are required to be
        /// consistent across multiple possible protocol implementations or service
        /// versions.
        /// </summary>
        public void Copy(XRoadMessage message)
        {
            ServiceManager = message.ServiceManager;
            Header = message.Header;
        }

        /// <summary>
        /// Get type lookup object which is used to serialize or deserialize current X-Road message.
        /// </summary>
        public ISerializer GetSerializer() => ServiceManager?.GetSerializer(Version);

        /// <summary>
        /// Enable filtering for X-Road message, using the filter specified by name.
        /// </summary>
        public void EnableFilter(string filterName)
        {
            EnableFiltering = true;
            FilterName = filterName;
        }
    }
}