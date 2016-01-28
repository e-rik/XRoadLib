﻿using System;
using System.Collections.Generic;
using System.Xml;
using XRoadLib.Extensions;
using XRoadLib.Schema;
using XRoadLib.Serialization.Template;

namespace XRoadLib.Serialization.Mapping
{
    public class SequenceTypeMap<T> : TypeMap<T> where T : class, IXRoadSerializable, new()
    {
        private readonly ISerializerCache serializerCache;
        private readonly IList<IPropertyMap> propertyMaps = new List<IPropertyMap>();
        private readonly TypeDefinition typeDefinition;

        public override bool IsSimpleType => false;

        public SequenceTypeMap(ISerializerCache serializerCache, TypeDefinition typeDefinition)
        {
            this.serializerCache = serializerCache;
            this.typeDefinition = typeDefinition;
        }

        public override object Deserialize(XmlReader reader, IXmlTemplateNode templateNode, SerializationContext context)
        {
            var entity = new T();
            entity.SetTemplateMembers(templateNode.ChildNames);

            if (reader.IsEmptyElement)
                return entity;

            var depth = reader.Depth;
            var properties = propertyMaps.GetEnumerator();

            while (reader.Read() && depth < reader.Depth)
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                MoveToProperty(reader, properties);

                var childValidatorNode = templateNode[properties.Current.PropertyName, DtoVersion];
                if (childValidatorNode == null)
                {
                    reader.ReadToEndElement();
                    continue;
                }

                if (reader.IsNilElement() || properties.Current.Deserialize(reader, entity, childValidatorNode, context))
                    entity.OnMemberDeserialized(properties.Current.PropertyName);
            }

            return entity;
        }

        private void MoveToProperty(XmlReader reader, IEnumerator<IPropertyMap> properties)
        {
            while (properties.MoveNext())
                if (reader.LocalName == properties.Current.PropertyName)
                    return;

            throw XRoadException.InvalidQuery("Andmetüübil `{0}` puudub element `{1}` või see on esitatud vales kohas.", typeDefinition.Name, reader.LocalName);
        }

        public override void Serialize(XmlWriter writer, IXmlTemplateNode templateNode, object value, Type expectedType, SerializationContext context)
        {
            context.Protocol.Style.WriteType(writer, typeDefinition, expectedType);

            foreach (var propertyMap in propertyMaps)
            {
                var childTemplateNode = templateNode?[propertyMap.PropertyName, DtoVersion];
                if (templateNode == null || childTemplateNode != null)
                    propertyMap.Serialize(writer, childTemplateNode, value, context);
            }
        }

        public override void InitializeProperties(IDictionary<Type, ITypeMap> partialTypeMaps, IEnumerable<PropertyDefinition> propertyDefinitions)
        {
            if (propertyMaps.Count > 0)
                return;

            foreach (var propertyDefinition in propertyDefinitions)
            {
                var typeMap = propertyDefinition.TypeName != null
                    ? serializerCache.GetTypeMap(propertyDefinition.TypeName, propertyDefinition.ItemDefinition != null, DtoVersion)
                    : serializerCache.GetTypeMap(propertyDefinition.RuntimeInfo.PropertyType, DtoVersion, partialTypeMaps);

                propertyMaps.Add(new PropertyMap(serializerCache, propertyDefinition, typeMap));
            }
        }
    }
}