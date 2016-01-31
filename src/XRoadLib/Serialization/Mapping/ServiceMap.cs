﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using XRoadLib.Extensions;
using XRoadLib.Protocols;
using XRoadLib.Schema;
using XRoadLib.Serialization.Template;
using XRoadLib.Soap;

namespace XRoadLib.Serialization.Mapping
{
    public class ServiceMap : IServiceMap
    {
        private readonly IList<IParameterMap> parameters;
        private readonly IParameterMap result;

        public OperationDefinition OperationDefinition { get; }

        public ServiceMap(OperationDefinition operationDefinition, IList<IParameterMap> parameters, IParameterMap result)
        {
            this.parameters = parameters;
            this.result = result;

            OperationDefinition = operationDefinition;
        }

        public IDictionary<string, object> DeserializeRequest(XmlReader reader, SerializationContext context)
        {
            var requestName = context.Protocol.RequestPartNameInRequest;

            if (!reader.MoveToElement(3, requestName))
                throw XRoadException.InvalidQuery($"Päringus puudub X-tee `{requestName}` element.");

            return OperationDefinition.HasStrictContentOrder ? DeserializeParametersStrict(reader, context) : DeserializeParametersNonStrict(reader, context);
        }

        private IDictionary<string, object> DeserializeParametersStrict(XmlReader reader, SerializationContext context)
        {
            var parameterValues = new List<Tuple<string, object>>();
            var parameterNodes = context.XmlTemplate?.ParameterNodes.ToList();

            var parameterEnumerator = parameters.GetEnumerator();
            var templateNodeEnumerator = context.XmlTemplate?.ParameterNodes.GetEnumerator();

            while (parameterEnumerator.MoveNext() && (templateNodeEnumerator == null || templateNodeEnumerator.MoveNext()))
            {
                var parameterMap = parameterEnumerator.Current;
                var parameterInfo = parameterMap.Definition.ParameterInfo;
                var parameterNode = templateNodeEnumerator != null ? templateNodeEnumerator.Current : XRoadXmlTemplate.EmptyNode;

                object value;
                if (parameters.Count < 2)
                    parameterValues.Add(Tuple.Create(parameterInfo.Name, parameterMap.DeserializeRoot(reader, parameterNode, context)));
                else if (parameterMap.TryDeserialize(reader, parameterNode, context, out value))
                    parameterValues.Add(Tuple.Create(parameterInfo.Name, value));
                else
                {
                    parameterValues.Add(Tuple.Create(parameterInfo.Name, (object)null));
                    continue;
                }

                reader.Read();
            }

            if (parameterNodes != null)
                for (var i = 0; i < parameterNodes.Count; i++)
                    if (parameterNodes[i].IsRequired && (parameterValues.Count <= i || parameterValues[i].Item2 == null))
                        throw XRoadException.TeenuseKohustuslikParameeterPuudub(parameterValues[i].Item1);

            return parameterValues.ToDictionary(p => p.Item1, p => p.Item2);
        }

        private IDictionary<string, object> DeserializeParametersNonStrict(XmlReader reader, SerializationContext context)
        {
            var parameterValues = new Dictionary<string, object>();

            while (reader.Read() && reader.MoveToElement(4))
            {
                var parameter = parameters.SingleOrDefault(x => x.Definition.Name.LocalName == reader.LocalName);
                if (parameter == null)
                    throw XRoadException.InvalidQuery("Unexpected parameter `{0}` in operation `{1}` request.", reader.LocalName, OperationDefinition.Name.LocalName);

                var parameterInfo = parameter.Definition.ParameterInfo;
                var parameterNode = context.GetTemplateNode(parameterInfo.Name);

                parameterValues.Add(parameterInfo.Name, parameter.DeserializeRoot(reader, parameterNode, context));
            }

            if (context.XmlTemplate != null)
                foreach (var parameterNode in context.XmlTemplate.ParameterNodes.Where(n => n.IsRequired && (!parameterValues.ContainsKey(n.Name) || parameterValues[n.Name] == null)))
                    throw XRoadException.TeenuseKohustuslikParameeterPuudub(parameterNode.Name);

            return parameterValues;
        }

        public object DeserializeResponse(XmlReader reader, SerializationContext context)
        {
            var responseName = context.Protocol.ResponsePartNameInResponse;
            var parameterNode = context.XmlTemplate != null ? context.XmlTemplate.ResponseNode : XRoadXmlTemplate.EmptyNode;

            if (!reader.MoveToElement(2))
                throw XRoadException.InvalidQuery("No payload element in SOAP message.");

            if (reader.NamespaceURI == NamespaceConstants.SOAP_ENV && reader.LocalName == "Fault")
                return SoapMessageHelper.DeserializeSoapFault(reader);

            if (!reader.MoveToElement(3, responseName))
                throw XRoadException.InvalidQuery($"Expected payload element `{responseName}` was not found in SOAP message.");

            return result.DeserializeRoot(reader, parameterNode, context);
        }

        public void SerializeRequest(XmlWriter writer, IDictionary<string, object> values, SerializationContext context)
        {
            writer.WriteStartElement(OperationDefinition.Name.LocalName, OperationDefinition.Name.NamespaceName);
            writer.WriteStartElement(context.Protocol.RequestPartNameInRequest);

            foreach (var parameterMap in parameters)
            {
                var parameterName = parameterMap.Definition.Name.LocalName;

                object parameterValue = null;
                var hasValue = values.TryGetValue(parameterName, out parameterValue);

                if (parameterMap.Definition.IsOptional && !hasValue)
                    continue;

                parameterMap.Serialize(writer, context.GetTemplateNode(parameterName), parameterValue, context);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        public void SerializeResponse(XmlWriter writer, object value, SerializationContext context, XmlReader requestReader, ICustomSerialization customSerialization)
        {
            var containsRequest = requestReader.MoveToElement(2, OperationDefinition.Name.LocalName, OperationDefinition.Name.NamespaceName);

            if (containsRequest)
                writer.WriteStartElement(requestReader.Prefix, $"{OperationDefinition.Name.LocalName}Response", OperationDefinition.Name.NamespaceName);
            else writer.WriteStartElement($"{OperationDefinition.Name.LocalName}Response", OperationDefinition.Name.NamespaceName);

            var namespaceInContext = requestReader.NamespaceURI;
            if (containsRequest)
                CopyRequestToResponse(writer, requestReader, context, out namespaceInContext);

            if (result != null)
            {
                if (Equals(namespaceInContext, ""))
                    writer.WriteStartElement(context.Protocol.ResponsePartNameInResponse);
                else writer.WriteStartElement(context.Protocol.ResponsePartNameInResponse, "");

                var fault = value as IXRoadFault;
                if (fault == null)
                {
                    var responseNode = context.XmlTemplate != null ? context.XmlTemplate.ResponseNode : XRoadXmlTemplate.EmptyNode;
                    result.Serialize(writer, responseNode, value, context);
                }
                else SerializeFault(writer, fault, context.Protocol);

                writer.WriteEndElement();
            }

            customSerialization?.OnContentComplete(writer);

            writer.WriteEndElement();
        }

        private static void SerializeFault(XmlWriter writer, IXRoadFault fault, IProtocol protocol)
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

        private static void CopyRequestToResponse(XmlWriter writer, XmlReader reader, SerializationContext context, out string namespaceInContext)
        {
            namespaceInContext = reader.NamespaceURI;

            writer.WriteAttributes(reader, true);

            if (!reader.MoveToElement(3) || !reader.IsCurrentElement(3, context.Protocol.RequestPartNameInRequest))
                return;

            if (context.Protocol.RequestPartNameInRequest != context.Protocol.RequestPartNameInResponse)
            {
                writer.WriteStartElement(context.Protocol.RequestPartNameInResponse);
                writer.WriteAttributes(reader, true);

                while (reader.MoveToElement(4))
                    writer.WriteNode(reader, true);

                writer.WriteEndElement();
            }
            else writer.WriteNode(reader, true);
        }
    }
}