﻿using System.Xml;
using XRoadLib.Extensions;
using XRoadLib.Schema;
using XRoadLib.Serialization.Template;

namespace XRoadLib.Serialization.Mapping
{
    public class PropertyMap : IPropertyMap
    {
        private readonly bool hasAnonymousType;
        private readonly bool isFilterable;
        private readonly ISerializerCache serializerCache;
        private readonly GetValueMethod getValueMethod;
        private readonly SetValueMethod setValueMethod;
        private readonly ITypeMap typeMap;
        private readonly PropertyDefinition propertyDefinition;

        public string PropertyName => propertyDefinition.Name.LocalName;

        public PropertyMap(ISerializerCache serializerCache, PropertyDefinition propertyDefinition, ITypeMap typeMap)
        {
            this.propertyDefinition = propertyDefinition;
            this.serializerCache = serializerCache;
            this.typeMap = typeMap;

            hasAnonymousType = propertyDefinition.TypeDefinition.IsAnonymous;
            getValueMethod = propertyDefinition.RuntimeInfo.CreateGetValueMethod();
            setValueMethod = propertyDefinition.RuntimeInfo.CreateSetValueMethod();
            isFilterable = propertyDefinition.Owner.RuntimeInfo.IsFilterableField(PropertyName);
        }

        public bool Deserialize(XmlReader reader, IXRoadSerializable dtoObject, IXmlTemplateNode templateNode, SerializationContext context)
        {
            if (context.FilteringEnabled && !isFilterable)
            {
                reader.ReadToEndElement();
                return false;
            }

            string typeAttribute;
            if (hasAnonymousType && (typeAttribute = reader.GetAttribute("type", NamespaceConstants.XSI)) != null)
                throw XRoadException.InvalidQuery("Expected anonymous type, but `{0}` was given.", typeAttribute);

            var concreteTypeMap = hasAnonymousType || typeMap.IsSimpleType
                ? typeMap
                : (serializerCache.GetTypeMapFromXsiType(reader, typeMap.DtoVersion) ?? typeMap);

            var propertyValue = concreteTypeMap.Deserialize(reader, templateNode, context);
            if (propertyValue == null)
                return true;

            setValueMethod(dtoObject, propertyValue);

            return true;
        }

        public void Serialize(XmlWriter writer, IXmlTemplateNode templateNode, object value, SerializationContext context)
        {
            if (context.FilteringEnabled && !isFilterable)
                return;

            var propertyValue = getValueMethod(value);
            if (context.ExcludeNullElement && propertyValue == null)
                return;

            writer.WriteStartElement(PropertyName);

            if (propertyValue == null)
                writer.WriteNilAttribute();
            else
            {
                var concreteTypeMap = typeMap.IsSimpleType ? typeMap : serializerCache.GetTypeMap(propertyValue.GetType(), typeMap.DtoVersion);
                concreteTypeMap.Serialize(writer, templateNode, propertyValue, typeMap.RuntimeType, context);
            }

            writer.WriteEndElement();
        }
    }
}