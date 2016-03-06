﻿using System.Collections.Generic;
using System.Linq;
using System.Xml;
using XRoadLib.Extensions;
using XRoadLib.Schema;
using XRoadLib.Serialization.Template;

namespace XRoadLib.Serialization.Mapping
{
    public class PropertyMap : IPropertyMap
    {
        private readonly ISet<string> filters = new HashSet<string>();
        private readonly ISerializerCache serializerCache;
        private readonly ITypeMap typeMap;
        private readonly GetValueMethod getValueMethod;
        private readonly SetValueMethod setValueMethod;

        public PropertyDefinition Definition { get; }

        public PropertyMap(ISerializerCache serializerCache, PropertyDefinition propertyDefinition, ITypeMap typeMap, IEnumerable<string> availableFilters)
        {
            this.serializerCache = serializerCache;

            var contentTypeMap = typeMap as IContentTypeMap;
            this.typeMap = contentTypeMap != null && propertyDefinition.UseXop ? contentTypeMap.GetOptimizedContentTypeMap() : typeMap;

            Definition = propertyDefinition;

            getValueMethod = Definition.PropertyInfo.CreateGetValueMethod();
            setValueMethod = Definition.PropertyInfo.CreateSetValueMethod();

            if (availableFilters == null)
                return;

            foreach (var availableFilter in availableFilters.Where(f => Definition.DeclaringTypeDefinition.Type.IsFilterableField(Definition.RuntimeName, f)))
                filters.Add(availableFilter);
        }

        public bool Deserialize(XmlReader reader, IXRoadSerializable dtoObject, IXmlTemplateNode templateNode, XRoadMessage message)
        {
            if (message.EnableFiltering && !filters.Contains(message.FilterName))
            {
                if (reader.IsEmptyElement) reader.Read();
                else reader.ReadToEndElement();
                return false;
            }

            string typeAttribute;
            if (typeMap.Definition.IsAnonymous && !(typeMap is IArrayTypeMap) && (typeAttribute = reader.GetAttribute("type", NamespaceConstants.XSI)) != null)
                throw XRoadException.InvalidQuery($"Expected anonymous type, but `{typeAttribute}` was given.");

            var concreteTypeMap = (typeMap.Definition.IsInheritable ? serializerCache.GetTypeMapFromXsiType(reader) : null) ?? typeMap;

            var propertyValue = concreteTypeMap.Deserialize(reader, templateNode, Definition, message);
            if (propertyValue == null)
                return true;

            setValueMethod(dtoObject, propertyValue);

            return true;
        }

        public void Serialize(XmlWriter writer, IXmlTemplateNode templateNode, object value, XRoadMessage message)
        {
            if (message.EnableFiltering && !filters.Contains(message.FilterName))
                return;

            var propertyValue = value != null ? getValueMethod(value) : null;

            if (!Definition.MergeContent)
            {
                writer.WriteStartElement(Definition.Name.LocalName);

                if (propertyValue == null)
                    writer.WriteNilAttribute();
            }

            if (propertyValue != null)
            {
                var concreteTypeMap = typeMap.Definition.IsInheritable ? serializerCache.GetTypeMap(propertyValue.GetType()) : typeMap;
                concreteTypeMap.Serialize(writer, templateNode, propertyValue, Definition, message);
            }

            if (!Definition.MergeContent)
                writer.WriteEndElement();
        }
    }
}