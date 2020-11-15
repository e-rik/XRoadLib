using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace XRoadLib.Wsdl
{
    public abstract class DocumentableItem
    {
        protected abstract string ElementName { get; }

        public XmlElement DocumentationElement { get; set; }
        public List<XmlAttribute> ExtensibleAttributes { get; } = new List<XmlAttribute>();
        public List<ServiceDescriptionFormatExtension> Extensions { get; } = new List<ServiceDescriptionFormatExtension>();
        public Dictionary<string, string> Namespaces { get; } = new Dictionary<string, string>();

        public string Documentation
        {
            get => DocumentationElement != null ? DocumentationElement.InnerText : "";
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    DocumentationElement = null;
                    return;
                }

                var doc = new XmlDocument();
                DocumentationElement = doc.CreateElement(PrefixConstants.Wsdl, "documentation", NamespaceConstants.Wsdl);
                DocumentationElement.InnerText = value;
            }
        }

        internal void Write(XmlWriter writer)
        {
            WriteStartElement(writer, ElementName);
            WriteAttributes(writer);
            WriteElements(writer);
            writer.WriteEndElement();
        }

        protected virtual void WriteAttributes(XmlWriter writer)
        {
            Namespaces.Where(x => !string.IsNullOrWhiteSpace(x.Value) && writer.LookupPrefix(x.Value) != x.Key)
                      .ToList()
                      .ForEach(ns => writer.WriteAttributeString(PrefixConstants.Xmlns, ns.Key, NamespaceConstants.Xmlns, ns.Value));

            ExtensibleAttributes.ForEach(x => x.WriteTo(writer));
        }

        protected virtual void WriteElements(XmlWriter writer)
        {
            Extensions.ForEach(x => x.Write(writer));
            DocumentationElement?.WriteTo(writer);
        }

        protected void WriteStartElement(XmlWriter writer, string name)
        {
            var prefix =
                Namespaces.Where(kvp => kvp.Value == NamespaceConstants.Wsdl)
                          .Select(kvp => kvp.Key)
                          .SingleOrDefault()
                ?? writer.LookupPrefix(NamespaceConstants.Wsdl)
                ?? "";

            writer.WriteStartElement(prefix, name, NamespaceConstants.Wsdl);
        }
    }
}