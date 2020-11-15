using System.Xml;
using XRoadLib.Extensions;

namespace XRoadLib.Wsdl
{
    public class SoapHeaderBinding : ServiceDescriptionFormatExtension
    {
        public string Encoding { get; set; }
        public XmlQualifiedName Message { get; set; }
        public string Namespace { get; set; }
        public string Part { get; set; }
        public SoapBindingUse Use { get; set; } = SoapBindingUse.Default;

        internal override void Write(XmlWriter writer)
        {
            writer.WriteStartElement(PrefixConstants.Soap, "header", NamespaceConstants.Soap);

            writer.WriteQualifiedAttribute("message", Message);

            if (!string.IsNullOrEmpty(Encoding))
                writer.WriteAttributeString("encodingStyle", Encoding);

            if (!string.IsNullOrEmpty(Namespace))
                writer.WriteAttributeString("namespace", Namespace);

            if (!string.IsNullOrEmpty(Part))
                writer.WriteAttributeString("part", Part);

            if (Use != SoapBindingUse.Default)
                writer.WriteAttributeString("use", Use == SoapBindingUse.Encoded ? "encoded" : "literal");

            writer.WriteEndElement();
        }
    }
}