﻿using System.Xml;
using System.Xml.Linq;
using XRoadLib.Extensions;
using XRoadLib.Protocols;
using XRoadLib.Schema;

namespace XRoadLib.Serialization.Mapping
{
    public class ServiceMap : IServiceMap
    {
        private readonly ISerializerCache serializerCache;
        private readonly ITypeMap inputTypeMap;
        private readonly ITypeMap outputTypeMap;
        private readonly RequestValueDefinition requestValueDefinition;
        private readonly ResponseValueDefinition responseValueDefinition;

        public OperationDefinition Definition { get; }

        public bool HasParameters => requestValueDefinition.ParameterInfo != null;

        public ServiceMap(ISerializerCache serializerCache, OperationDefinition operationDefinition, RequestValueDefinition requestValueDefinition, ResponseValueDefinition responseValueDefinition, ITypeMap inputTypeMap, ITypeMap outputTypeMap)
        {
            this.serializerCache = serializerCache;
            this.inputTypeMap = inputTypeMap;
            this.outputTypeMap = outputTypeMap;
            this.requestValueDefinition = requestValueDefinition;
            this.responseValueDefinition = responseValueDefinition;

            Definition = operationDefinition;
        }

        public object DeserializeRequest(XmlReader reader, XRoadMessage message)
        {
            var requestName = message.Protocol.RequestPartNameInRequest;

            if (!requestValueDefinition.MergeContent && !reader.MoveToElement(3, requestName))
                throw XRoadException.InvalidQuery($"Päringus puudub X-tee `{requestName}` element.");

            if (requestValueDefinition.ParameterInfo != null)
                return inputTypeMap.Deserialize(reader, message.RequestNode, requestValueDefinition, message);

            return null;
        }

        public object DeserializeResponse(XmlReader reader, XRoadMessage message)
        {
            var responseName = message.Protocol.ResponsePartNameInResponse;

            if (!reader.MoveToElement(3, responseName))
                throw XRoadException.InvalidQuery($"Expected payload element `{responseName}` was not found in SOAP message.");

            var hasWrapperElement = HasWrapperResultElement(message);
            if (hasWrapperElement && !reader.MoveToElement(4, responseValueDefinition.Name.LocalName, responseValueDefinition.Name.NamespaceName))
                throw XRoadException.InvalidQuery($"Expected result wrapper element `{responseValueDefinition.Name}` was not found in SOAP message.");

            if (reader.IsNilElement())
            {
                reader.ReadToEndElement();
                return null;
            }

            string typeAttribute;
            if (outputTypeMap.Definition.IsAnonymous && !(outputTypeMap is IArrayTypeMap) && (typeAttribute = reader.GetAttribute("type", NamespaceConstants.XSI)) != null)
                throw XRoadException.InvalidQuery($"Expected anonymous type, but `{typeAttribute}` was given.");

            var concreteTypeMap = (outputTypeMap.Definition.IsInheritable ? serializerCache.GetTypeMapFromXsiType(reader) : null) ?? outputTypeMap;

            return concreteTypeMap.Deserialize(reader, message.ResponseNode, responseValueDefinition, message);
        }

        public void SerializeRequest(XmlWriter writer, object value, XRoadMessage message, string requestNamespace = null)
        {
            var ns = string.IsNullOrEmpty(requestNamespace) ? Definition.Name.NamespaceName : requestNamespace;
            var addPrefix = writer.LookupPrefix(ns) == null;

            if (addPrefix) writer.WriteStartElement(PrefixConstants.TARGET, Definition.Name.LocalName, ns);
            else writer.WriteStartElement(Definition.Name.LocalName, ns);

            if (!requestValueDefinition.MergeContent)
                writer.WriteStartElement(message.Protocol.RequestPartNameInRequest);

            if (requestValueDefinition.ParameterInfo != null)
                inputTypeMap.Serialize(writer, message.RequestNode, value, requestValueDefinition, message);

            if (!requestValueDefinition.MergeContent)
                writer.WriteEndElement();

            writer.WriteEndElement();
        }

        public void SerializeResponse(XmlWriter writer, object value, XRoadMessage message, XmlReader requestReader, ICustomSerialization customSerialization)
        {
            var containsRequest = requestReader.MoveToElement(2, Definition.Name.LocalName, Definition.Name.NamespaceName);

            if (containsRequest)
                writer.WriteStartElement(requestReader.Prefix, $"{Definition.Name.LocalName}Response", Definition.Name.NamespaceName);
            else writer.WriteStartElement($"{Definition.Name.LocalName}Response", Definition.Name.NamespaceName);

            var fault = value as IXRoadFault;
            var namespaceInContext = requestReader.NamespaceURI;

            if (containsRequest && !Definition.ProhibitRequestPartInResponse && (message.Protocol.NonTechnicalFaultInResponseElement || fault == null))
                CopyRequestToResponse(writer, requestReader, message, out namespaceInContext);

            if (!message.Protocol.NonTechnicalFaultInResponseElement && fault != null)
                SerializeFault(writer, fault, message.Protocol);
            else if (outputTypeMap != null)
            {
                if (Equals(namespaceInContext, ""))
                    writer.WriteStartElement(message.Protocol.ResponsePartNameInResponse);
                else writer.WriteStartElement(message.Protocol.ResponsePartNameInResponse, "");

                if (fault != null)
                    SerializeFault(writer, fault, message.Protocol);
                else if (outputTypeMap != null)
                {
                    var addWrapperElement = HasWrapperResultElement(message);

                    if (addWrapperElement)
                        writer.WriteStartElement(responseValueDefinition.Name.LocalName, responseValueDefinition.Name.NamespaceName);

                    if (value == null)
                        writer.WriteNilAttribute();
                    else
                    {
                        var concreteTypeMap = outputTypeMap.Definition.IsInheritable ? serializerCache.GetTypeMap(value.GetType()) : outputTypeMap;

                        concreteTypeMap.Serialize(writer, message.ResponseNode, value, responseValueDefinition, message);
                    }

                    if (addWrapperElement)
                        writer.WriteEndElement();
                }

                writer.WriteEndElement();

                customSerialization?.OnContentComplete(writer);
            }

            writer.WriteEndElement();
        }

        private bool HasWrapperResultElement(XRoadMessage message)
        {
            return !responseValueDefinition.MergeContent
                   && responseValueDefinition.XRoadFaultPresentation != XRoadFaultPresentation.Implicit
                   && message.Protocol.NonTechnicalFaultInResponseElement;
        }

        private static void SerializeFault(XmlWriter writer, IXRoadFault fault, XRoadProtocol protocol)
        {
            writer.WriteStartElement("faultCode");
            protocol.Style.WriteExplicitType(writer, XName.Get("string", NamespaceConstants.XSD));
            writer.WriteValue(fault.FaultCode);
            writer.WriteEndElement();

            writer.WriteStartElement("faultString");
            protocol.Style.WriteExplicitType(writer, XName.Get("string", NamespaceConstants.XSD));
            writer.WriteValue(fault.FaultString);
            writer.WriteEndElement();
        }

        private static void CopyRequestToResponse(XmlWriter writer, XmlReader reader, XRoadMessage message, out string namespaceInContext)
        {
            namespaceInContext = reader.NamespaceURI;

            writer.WriteAttributes(reader, true);

            if (!reader.MoveToElement(3) || !reader.IsCurrentElement(3, message.Protocol.RequestPartNameInRequest))
                return;

            if (message.Protocol.RequestPartNameInRequest != message.Protocol.RequestPartNameInResponse)
            {
                writer.WriteStartElement(message.Protocol.RequestPartNameInResponse);
                writer.WriteAttributes(reader, true);

                while (reader.MoveToElement(4))
                    writer.WriteNode(reader, true);

                writer.WriteEndElement();
            }
            else writer.WriteNode(reader, true);
        }
    }
}